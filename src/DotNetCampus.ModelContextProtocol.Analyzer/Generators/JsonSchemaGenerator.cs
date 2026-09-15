using System.Collections.Immutable;
using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.Generators.Builders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using DotNetCampus.ModelContextProtocol.Generators.SourceBuilders;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class JsonSchemaGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var jsonSerializerContextProvider = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax classDeclarationSyntax
                                    && HasAttribute(classDeclarationSyntax, "GenerateJsonSchema"),
                static (syntaxContext, cancellationToken) => CreateModels(
                    (ClassDeclarationSyntax)syntaxContext.Node,
                    syntaxContext.SemanticModel,
                    cancellationToken))
            .Where(static models => models.Length > 0);

        context.RegisterSourceOutput(jsonSerializerContextProvider, Execute);
    }

    private static ImmutableArray<GenerateJsonSchemaGeneratingModel> CreateModels(
        ClassDeclarationSyntax classDeclarationSyntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var namespaceName = GetNamespace(classDeclarationSyntax);
        var jsonSerializerContextName = classDeclarationSyntax.Identifier.ValueText;
        var jsonSerializerContextFullName = GetFullName(classDeclarationSyntax, namespaceName);
        var isJsonSerializerContextPublic = IsEffectivelyPublic(classDeclarationSyntax);
        var models = ImmutableArray.CreateBuilder<GenerateJsonSchemaGeneratingModel>();
        var generatedTypes = new HashSet<string>();

        foreach (var attribute in GetAttributes(classDeclarationSyntax, "JsonSerializable"))
        {
            foreach (var typeSyntax in GetJsonSerializableTypeSyntaxes(attribute))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (semanticModel.GetTypeInfo(typeSyntax, cancellationToken).Type is not { } type)
                {
                    continue;
                }

                var typeKey = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!generatedTypes.Add(typeKey))
                {
                    continue;
                }

                models.Add(new GenerateJsonSchemaGeneratingModel
                {
                    Namespace = namespaceName,
                    JsonSerializerContextFullName = jsonSerializerContextFullName,
                    JsonSerializerContextName = jsonSerializerContextName,
                    IsJsonSerializerContextPublic = isJsonSerializerContextPublic,
                    Type = type,
                });
            }
        }

        return models.ToImmutable();
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<GenerateJsonSchemaGeneratingModel> models)
    {
        foreach (var model in models)
        {
            using var builder = new SourceTextBuilder(model.Namespace)
                {
                    RemoveIndentForPreprocessorLines = true,
                }
                .AddTypeDeclaration($"{model.GetAccessModifier()} static class {model.GetExtensionTypeName()}", t => t
                    .WithSummaryComment($"为 {model.JsonSerializerContextFullName} 生成 JSON Schema 扩展方法。")
                    .AddGetCompilerGeneratedJsonSchemaMethod(model));

            var code = builder.ToString();
            context.AddSource($"{model.JsonSerializerContextName}.{model.Type.ToDeclarationNestedDisplayString()}.g.cs", code);
        }
    }

    private static bool HasAttribute(ClassDeclarationSyntax classDeclarationSyntax, string attributeName)
    {
        return GetAttributes(classDeclarationSyntax, attributeName).Any();
    }

    private static IEnumerable<AttributeSyntax> GetAttributes(ClassDeclarationSyntax classDeclarationSyntax, string attributeName)
    {
        return classDeclarationSyntax.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute => IsAttributeName(attribute.Name, attributeName));
    }

    private static IEnumerable<TypeSyntax> GetJsonSerializableTypeSyntaxes(AttributeSyntax attributeSyntax)
    {
        if (attributeSyntax.ArgumentList is not { } argumentList)
        {
            yield break;
        }

        foreach (var argument in argumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOfExpressionSyntax)
            {
                yield return typeOfExpressionSyntax.Type;
            }
        }
    }

    private static bool IsAttributeName(NameSyntax nameSyntax, string attributeName)
    {
        var actualName = GetRightmostName(nameSyntax);
        return actualName == attributeName || actualName == $"{attributeName}Attribute";
    }

    private static string GetRightmostName(NameSyntax nameSyntax)
    {
        return nameSyntax switch
        {
            IdentifierNameSyntax identifierNameSyntax => identifierNameSyntax.Identifier.ValueText,
            GenericNameSyntax genericNameSyntax => genericNameSyntax.Identifier.ValueText,
            QualifiedNameSyntax qualifiedNameSyntax => GetRightmostName(qualifiedNameSyntax.Right),
            AliasQualifiedNameSyntax aliasQualifiedNameSyntax => GetRightmostName(aliasQualifiedNameSyntax.Name),
            _ => nameSyntax.ToString().Split('.').Last(),
        };
    }

    private static string GetNamespace(SyntaxNode syntaxNode)
    {
        var names = new Stack<string>();
        for (var parent = syntaxNode.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclarationSyntax:
                    names.Push(namespaceDeclarationSyntax.Name.ToString());
                    break;
            }
        }

        return string.Join(".", names);
    }

    private static string GetFullName(ClassDeclarationSyntax classDeclarationSyntax, string namespaceName)
    {
        var names = new Stack<string>();
        for (SyntaxNode? current = classDeclarationSyntax; current is not null; current = current.Parent)
        {
            if (current is ClassDeclarationSyntax classSyntax)
            {
                names.Push(classSyntax.Identifier.ValueText);
            }
        }

        var typeName = string.Join(".", names);
        return string.IsNullOrWhiteSpace(namespaceName)
            ? typeName
            : $"{namespaceName}.{typeName}";
    }

    private static bool IsEffectivelyPublic(ClassDeclarationSyntax classDeclarationSyntax)
    {
        for (SyntaxNode? current = classDeclarationSyntax; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax typeDeclarationSyntax &&
                !typeDeclarationSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                return false;
            }
        }

        return true;
    }
}
