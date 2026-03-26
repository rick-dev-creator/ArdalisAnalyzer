using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static ArdalisAnalyzer.Analyzer.ResultAnalyzerHelpers;

namespace ArdalisAnalyzer.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ResultImplicitConversionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ARDRES002";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Result<T> implicitly converted to T without checking status",
            messageFormat: "'{0}' is implicitly converted from Result<{1}> to {1} without verifying the result status first",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Implicit conversion from Result<T> to T extracts the Value without checking IsSuccess or Status, which can lead to NullReferenceException or silent data corruption.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Catch: string name = result;
            context.RegisterSyntaxNodeAction(AnalyzeAssignment,
                SyntaxKind.SimpleAssignmentExpression);

            // Catch: var name = (string)result; or string name = result;
            context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration,
                SyntaxKind.VariableDeclaration);

            // Catch: return result; (when method returns T, not Result<T>)
            context.RegisterSyntaxNodeAction(AnalyzeReturn,
                SyntaxKind.ReturnStatement);

            // Catch: SomeMethod(result) where parameter is T
            context.RegisterSyntaxNodeAction(AnalyzeArgument,
                SyntaxKind.Argument);
        }

        // ---------------------------------------------------------------
        //  string name = result;  /  name = result;
        // ---------------------------------------------------------------

        private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (AssignmentExpressionSyntax)context.Node;
            CheckImplicitConversion(context, assignment.Right);
        }

        // ---------------------------------------------------------------
        //  string name = result;  /  var name = result; (declaration)
        // ---------------------------------------------------------------

        private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
        {
            var declaration = (VariableDeclarationSyntax)context.Node;
            foreach (var variable in declaration.Variables)
            {
                if (variable.Initializer != null)
                {
                    CheckImplicitConversion(context, variable.Initializer.Value);
                }
            }
        }

        // ---------------------------------------------------------------
        //  return result;
        // ---------------------------------------------------------------

        private static void AnalyzeReturn(SyntaxNodeAnalysisContext context)
        {
            var returnStatement = (ReturnStatementSyntax)context.Node;
            if (returnStatement.Expression != null)
            {
                CheckImplicitConversion(context, returnStatement.Expression);
            }
        }

        // ---------------------------------------------------------------
        //  SomeMethod(result)
        // ---------------------------------------------------------------

        private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
        {
            var argument = (ArgumentSyntax)context.Node;
            CheckImplicitConversion(context, argument.Expression);
        }

        // ---------------------------------------------------------------
        //  Core: detect implicit conversion from Result<T> to T
        // ---------------------------------------------------------------

        private static void CheckImplicitConversion(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression)
        {
            var conversion = context.SemanticModel.GetConversion(expression, context.CancellationToken);

            // Must be an implicit user-defined conversion
            if (!conversion.IsImplicit || !conversion.IsUserDefined)
                return;

            var sourceType = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
            if (!IsArdalisResultType(sourceType))
                return;

            // Get the target type (T from Result<T>)
            var namedType = sourceType as INamedTypeSymbol;
            if (namedType == null || !namedType.IsGenericType || namedType.TypeArguments.Length == 0)
                return;

            var innerType = namedType.TypeArguments[0];

            // Check the converted-to type matches T (Result<T> -> T conversion)
            var convertedType = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).ConvertedType;
            if (!SymbolEqualityComparer.Default.Equals(convertedType, innerType))
                return;

            // Check if guarded
            var resultIdentifier = GetResultIdentifier(expression);
            if (resultIdentifier != null && IsGuarded(expression, resultIdentifier))
                return;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, expression.GetLocation(),
                    expression.ToString(),
                    innerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }
}
