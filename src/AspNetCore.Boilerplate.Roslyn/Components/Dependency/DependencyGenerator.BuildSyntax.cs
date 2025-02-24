using System.Collections.Immutable;
using AspNetCore.Boilerplate.Roslyn.Constants;
using AspNetCore.Boilerplate.Roslyn.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AspNetCore.Boilerplate.Roslyn.Components.Dependency;

partial class DependencyGenerator
{
    private static class BuildSyntax
    {
        public static CompilationUnitSyntax GetCompilationUnitForDependency(
            HierarchyInfo hierarchyInfo,
            ImmutableArray<ExpressionStatementSyntax> registerExpressions
        )
        {
            TypeDeclarationSyntax typeDeclarationSyntax = (
                (ClassDeclarationSyntax)
                    hierarchyInfo
                        .Hierarchy[0]
                        .GetSyntax()
                        .AddModifiers(Token(SyntaxKind.PartialKeyword))
                        .AddBaseListTypes(
                            SimpleBaseType(
                                IdentifierName(GeneratorConstant.Namespace + ".IAutoRegister")
                            )
                        )
            ).AddMembers(
                MethodDeclaration(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)),
                        Identifier("AddDependencies")
                    )
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(
                        ParameterList(
                            SingletonSeparatedList(
                                Parameter(Identifier("services"))
                                    .WithType(IdentifierName("IServiceCollection"))
                            )
                        )
                    )
                    .AddBodyStatements(registerExpressions.ToArray<StatementSyntax>())
            );

            var hierarchySpan = hierarchyInfo.Hierarchy.AsSpan();
            foreach (var parentType in hierarchySpan.Slice(1, hierarchySpan.Length - 1))
                typeDeclarationSyntax = parentType
                    .GetSyntax()
                    .AddModifiers(Token(SyntaxKind.PartialKeyword))
                    .AddMembers(typeDeclarationSyntax);

            var syntaxTriviaList = TriviaList(
                Comment("// <auto-generated/>"),
                Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)),
                Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true))
            );
            var unitSyntax = CompilationUnit()
                .WithUsings(
                    SingletonList(
                        UsingDirective(IdentifierName("Microsoft.Extensions.DependencyInjection"))
                    )
                );

            if (hierarchyInfo.Namespace is "")
                return unitSyntax
                    .AddMembers(typeDeclarationSyntax.WithLeadingTrivia(syntaxTriviaList))
                    .NormalizeWhitespace();

            return unitSyntax
                .AddMembers(
                    NamespaceDeclaration(IdentifierName(hierarchyInfo.Namespace))
                        .AddMembers(typeDeclarationSyntax)
                        .WithLeadingTrivia(syntaxTriviaList)
                )
                .NormalizeWhitespace();
        }
    }
}
