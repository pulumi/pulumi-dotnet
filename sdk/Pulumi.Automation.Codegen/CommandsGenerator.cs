// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// Generates one method per executable CLI command. Each method builds the
    /// argument vector for that command from a positional argument list and a
    /// strongly-typed options bag (the classes emitted by
    /// <see cref="OptionsGenerator"/>).
    /// </summary>
    /// <remarks>
    /// The generated method body mirrors the other SDK generators: the
    /// subcommand chain, then preset flags, then the user's flags in
    /// alphabetical order, then the positional arguments behind a <c>--</c>
    /// separator. Flags and positionals that are unset are not emitted.
    /// </remarks>
    public static class CommandsGenerator
    {
        private const string Final = "__final";
        private const string Arguments = "__arguments";
        private const string Item = "__item";
        private const string Value = "__value";
        private const string Options = "options";

        /// <summary>
        /// Generates the command methods for every executable command in the
        /// given CLI specification, as a single C# source file.
        /// </summary>
        public static string Generate(CommandTreeNode root, string namespaceName)
        {
            var commands = CommandTreeWalker.Walk(root);

            var byName = new Dictionary<string, ResolvedCommand>(StringComparer.Ordinal);
            foreach (var command in commands)
            {
                if (byName.TryGetValue(command.Identifier, out var existing))
                {
                    throw new InvalidOperationException(
                        $"The commands `{existing.CliCommand}` and `{command.CliCommand}` both produce " +
                        $"a method named {command.Identifier}.");
                }

                byName.Add(command.Identifier, command);
            }

            var api = ClassDeclaration("API")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword)))
                .WithMembers(List<MemberDeclarationSyntax>(commands.Select(EmitMethod)))
                .WithLeadingTrivia(Emitter.SummaryComment(
                    "Builds Pulumi CLI invocations, one method per command."));

            var namespaceDeclaration = NamespaceDeclaration(ParseName(namespaceName))
                .WithMembers(SingletonList<MemberDeclarationSyntax>(api));

            var usings = new List<UsingDirectiveSyntax>
            {
                UsingDirective(ParseName("System.Collections.Generic")),
            };
            if (commands.Any(command => command.VisibleFlags.Any(flag => flag.Type == "int")))
            {
                usings.Add(UsingDirective(ParseName("System.Globalization")));
            }

            var unit = CompilationUnit()
                .WithUsings(List(usings))
                .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));

            return Emitter.Render(unit);
        }

        private static MethodDeclarationSyntax EmitMethod(ResolvedCommand command)
        {
            var statements = new List<StatementSyntax>
            {
                // var __final = new List<string> { "org", "search", ... };
                Declare(Final, NewList(command.Breadcrumbs.Select(StringLiteral))),
            };

            foreach (var flag in command.PresetFlags)
            {
                statements.AddRange(EmitPreset(flag));
            }

            foreach (var flag in command.VisibleFlags)
            {
                statements.AddRange(EmitFlag(flag));
            }

            var positionals = command.Arguments?.Arguments ?? new List<PositionalArgument>();
            if (positionals.Count > 0)
            {
                statements.Add(Declare(Arguments, NewList()));
                for (var index = 0; index < positionals.Count; index++)
                {
                    var required = index < command.Arguments!.RequiredArguments;
                    var variadic = index == positionals.Count - 1 && command.Arguments.Variadic;
                    statements.Add(EmitPositional(Names.CamelCase(positionals[index].Name), required, variadic));
                }

                // if (__arguments.Count > 0) { __final.Add("--"); __final.AddRange(__arguments); }
                statements.Add(IfStatement(
                    BinaryExpression(
                        SyntaxKind.GreaterThanExpression,
                        MemberAccess(IdentifierName(Arguments), "Count"),
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
                    Block(
                        AddLiteral(Final, "--"),
                        ExpressionStatement(Invoke(Final, "AddRange", IdentifierName(Arguments))))));
            }

            statements.Add(ReturnStatement(IdentifierName(Final)));

            return MethodDeclaration(ParseTypeName("IReadOnlyList<string>"), Identifier(command.Identifier))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(EmitParameters(command))
                .WithBody(Block(statements))
                .WithLeadingTrivia(Emitter.SummaryComment(
                    $"Builds the arguments for the <c>{command.CliCommand}</c> command."));
        }

        private static ParameterListSyntax EmitParameters(ResolvedCommand command)
        {
            var parameters = new List<ParameterSyntax>();
            var positionals = command.Arguments?.Arguments ?? new List<PositionalArgument>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < positionals.Count; index++)
            {
                var argument = positionals[index];
                var name = Names.CamelCase(argument.Name);

                if (name == Options)
                {
                    throw new InvalidOperationException(
                        $"The positional argument {argument.Name} of `{command.CliCommand}` collides with the " +
                        "reserved parameter name 'options'.");
                }

                if (!seen.Add(name))
                {
                    throw new InvalidOperationException(
                        $"Two positional arguments of `{command.CliCommand}` map to the parameter name {name}.");
                }

                var required = index < command.Arguments!.RequiredArguments;
                var variadic = index == positionals.Count - 1 && command.Arguments.Variadic;
                var scalar = Types.Scalar(argument.Type, argument.Name);
                var type = variadic ? $"IEnumerable<{scalar}>" : scalar;

                var parameter = Parameter(Identifier(name)).WithType(ParseTypeName(required ? type : type + "?"));
                if (!required)
                {
                    parameter = parameter.WithDefault(EqualsValueClause(Null()));
                }

                parameters.Add(parameter);
            }

            parameters.Add(Parameter(Identifier(Options))
                .WithType(ParseTypeName(command.OptionsClassName + "?"))
                .WithDefault(EqualsValueClause(Null())));

            return ParameterList(SeparatedList(parameters));
        }

        private static IEnumerable<StatementSyntax> EmitFlag(Flag flag)
        {
            var token = "--" + flag.Name;
            var access = OptionAccess(flag.Name);

            // A pattern variable declared in an `if` condition leaks into the
            // enclosing method body, so each captured flag needs its own name.
            var captured = Value + Names.PascalCase(flag.Name);

            if (flag.Repeatable)
            {
                // if (options?.Flag is { } __valueFlag)
                //     foreach (var __item in __valueFlag) { __final.Add("--flag"); __final.Add(__item); }
                yield return IfStatement(
                    IsPattern(access, CaptureNonNull(captured)),
                    Block(ForEachStatement(
                        IdentifierName("var"),
                        Identifier(Item),
                        IdentifierName(captured),
                        Block(AddLiteral(Final, token), Add(Final, IdentifierName(Item))))));
            }
            else if (flag.Type == "boolean")
            {
                // if (options?.Flag is true) __final.Add("--flag");
                yield return IfStatement(
                    IsPattern(access, ConstantPattern(LiteralExpression(SyntaxKind.TrueLiteralExpression))),
                    Block(AddLiteral(Final, token)));
            }
            else if (flag.Type == "int")
            {
                // if (options?.Flag is { } __valueFlag) { __final.Add("--flag"); __final.Add(__valueFlag.ToString(CultureInfo.InvariantCulture)); }
                yield return IfStatement(
                    IsPattern(access, CaptureNonNull(captured)),
                    Block(AddLiteral(Final, token), Add(Final, InvariantToString(IdentifierName(captured)))));
            }
            else
            {
                // if (options?.Flag is { } __valueFlag) { __final.Add("--flag"); __final.Add(__valueFlag); }
                yield return IfStatement(
                    IsPattern(access, CaptureNonNull(captured)),
                    Block(AddLiteral(Final, token), Add(Final, IdentifierName(captured))));
            }
        }

        private static IEnumerable<StatementSyntax> EmitPreset(Flag flag)
        {
            var adds = PresetAdds(flag).ToList();
            if (adds.Count == 0)
            {
                yield break;
            }

            if (flag.Omit)
            {
                // The flag is hidden from users, so the preset is always applied.
                foreach (var statement in adds)
                {
                    yield return statement;
                }
            }
            else
            {
                // The flag is user-overridable, so apply the preset only when
                // the user did not set it; the option loop emits their value.
                yield return IfStatement(
                    IsPattern(OptionAccess(flag.Name), ConstantPattern(Null())),
                    Block(adds));
            }
        }

        private static IEnumerable<StatementSyntax> PresetAdds(Flag flag)
        {
            var token = "--" + flag.Name;
            var preset = flag.Preset!.Value;

            switch (preset.ValueKind)
            {
                case JsonValueKind.True:
                    yield return AddLiteral(Final, token);
                    break;
                case JsonValueKind.False:
                    break;
                case JsonValueKind.String:
                    yield return AddLiteral(Final, token);
                    yield return AddLiteral(Final, preset.GetString()!);
                    break;
                case JsonValueKind.Number:
                    yield return AddLiteral(Final, token);
                    yield return AddLiteral(Final, preset.GetRawText());
                    break;
                case JsonValueKind.Array:
                    foreach (var element in preset.EnumerateArray())
                    {
                        yield return AddLiteral(Final, token);
                        yield return AddLiteral(Final, element.GetString()!);
                    }

                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported preset value for flag --{flag.Name}: {preset.GetRawText()}.");
            }
        }

        private static StatementSyntax EmitPositional(string name, bool required, bool variadic)
        {
            if (variadic)
            {
                var loop = ForEachStatement(
                    IdentifierName("var"),
                    Identifier(Item),
                    IdentifierName(name),
                    Block(Add(Arguments, IdentifierName(Item))));

                // A required variadic is non-nullable, so it needs no guard.
                return required ? loop : IfStatement(NotNull(name), Block(loop));
            }

            if (required)
            {
                return Add(Arguments, IdentifierName(name));
            }

            return IfStatement(NotNull(name), Block(Add(Arguments, IdentifierName(name))));
        }

        // Expression and statement factories.

        private static ExpressionSyntax OptionAccess(string flagName)
            => ConditionalAccessExpression(
                IdentifierName(Options),
                MemberBindingExpression(IdentifierName(Names.PascalCase(flagName))));

        private static PatternSyntax CaptureNonNull(string variable)
            => RecursivePattern()
                .WithPropertyPatternClause(PropertyPatternClause())
                .WithDesignation(SingleVariableDesignation(Identifier(variable)));

        private static ExpressionSyntax IsPattern(ExpressionSyntax expression, PatternSyntax pattern)
            => IsPatternExpression(expression, pattern);

        private static ExpressionSyntax InvariantToString(ExpressionSyntax expression)
            => InvocationExpression(
                MemberAccess(expression, "ToString"),
                ArgumentList(SingletonSeparatedList(Argument(
                    MemberAccess(IdentifierName("CultureInfo"), "InvariantCulture")))));

        private static ExpressionSyntax NotNull(string identifier)
            => BinaryExpression(SyntaxKind.NotEqualsExpression, IdentifierName(identifier), Null());

        private static StatementSyntax Add(string list, ExpressionSyntax argument)
            => ExpressionStatement(Invoke(list, "Add", argument));

        private static StatementSyntax AddLiteral(string list, string value)
            => Add(list, StringLiteral(value));

        private static InvocationExpressionSyntax Invoke(string target, string method, ExpressionSyntax argument)
            => InvocationExpression(
                MemberAccess(IdentifierName(target), method),
                ArgumentList(SingletonSeparatedList(Argument(argument))));

        private static MemberAccessExpressionSyntax MemberAccess(ExpressionSyntax expression, string name)
            => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, expression, IdentifierName(name));

        private static StatementSyntax Declare(string name, ExpressionSyntax value)
            => LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(name))
                    .WithInitializer(EqualsValueClause(value)))));

        private static ExpressionSyntax NewList(IEnumerable<ExpressionSyntax>? elements = null)
        {
            var creation = ObjectCreationExpression(ParseTypeName("List<string>"));
            return elements == null
                ? creation.WithArgumentList(ArgumentList())
                : creation.WithInitializer(InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SeparatedList(elements)));
        }

        private static LiteralExpressionSyntax StringLiteral(string value)
            => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(value));

        private static LiteralExpressionSyntax Null()
            => LiteralExpression(SyntaxKind.NullLiteralExpression);
    }
}
