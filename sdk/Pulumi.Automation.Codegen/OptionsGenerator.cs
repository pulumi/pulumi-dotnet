// Copyright 2016-2026, Pulumi Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Pulumi.Automation.Codegen
{
    /// <summary>
    /// Generates one options class per executable CLI command from a CLI
    /// specification.
    /// </summary>
    /// <remarks>
    /// Flags are flattened: each command's options type contains the flags
    /// declared on the command itself plus those inherited from its ancestors,
    /// with the closest declaration winning when names collide. Override
    /// information (omit and preset) applies only at the node that declares
    /// it and is stripped during descent; flags that remain omitted are not
    /// exposed as properties.
    /// </remarks>
    public static class OptionsGenerator
    {
        /// <summary>
        /// Generates the options classes for every executable command in the
        /// given CLI specification, as a single C# source file.
        /// </summary>
        public static string Generate(CommandTreeNode root, string namespaceName)
        {
            var commands = CommandTreeWalker.Walk(root);

            var byName = new Dictionary<string, ResolvedCommand>(StringComparer.Ordinal);
            foreach (var command in commands)
            {
                if (byName.TryGetValue(command.OptionsClassName, out var existing))
                {
                    throw new InvalidOperationException(
                        $"The commands `{existing.CliCommand}` and `{command.CliCommand}` both produce " +
                        $"an options class named {command.OptionsClassName}.");
                }

                byName.Add(command.OptionsClassName, command);
            }

            var namespaceDeclaration = NamespaceDeclaration(ParseName(namespaceName))
                .WithMembers(List<MemberDeclarationSyntax>(commands.Select(EmitClass)));

            var unit = CompilationUnit()
                .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));

            if (commands.Any(command => command.VisibleFlags.Any(flag => flag.Repeatable)))
            {
                unit = unit.WithUsings(SingletonList(UsingDirective(ParseName("System.Collections.Generic"))));
            }

            return Emitter.Render(unit);
        }

        private static ClassDeclarationSyntax EmitClass(ResolvedCommand command)
        {
            var properties = new List<MemberDeclarationSyntax>();
            var propertyNames = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var flag in command.VisibleFlags)
            {
                var propertyName = Names.PascalCase(flag.Name);

                if (propertyName == command.OptionsClassName)
                {
                    throw new InvalidOperationException(
                        $"The flag --{flag.Name} of `{command.CliCommand}` produces a property named like " +
                        $"its containing class ({propertyName}).");
                }

                if (propertyNames.TryGetValue(propertyName, out var existing))
                {
                    throw new InvalidOperationException(
                        $"The flags --{existing} and --{flag.Name} of `{command.CliCommand}` both produce " +
                        $"a property named {propertyName}.");
                }

                propertyNames.Add(propertyName, flag.Name);

                var property = PropertyDeclaration(ParseTypeName(Types.OptionProperty(flag)), Identifier(propertyName))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(AccessorList(List(new[]
                    {
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                    })));

                if (!string.IsNullOrEmpty(flag.Description))
                {
                    property = property.WithLeadingTrivia(Emitter.SummaryComment(flag.Description.Split('\n'), escape: true));
                }

                properties.Add(property);
            }

            return ClassDeclaration(command.OptionsClassName)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.SealedKeyword)))
                .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                    SimpleBaseType(ParseTypeName("BaseOptions")))))
                .WithMembers(List(properties))
                .WithLeadingTrivia(Emitter.SummaryComment($"Options for the <c>{command.CliCommand}</c> command."));
        }
    }
}
