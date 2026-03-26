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

            // Catch: name = result;
            context.RegisterSyntaxNodeAction(AnalyzeAssignment,
                SyntaxKind.SimpleAssignmentExpression);

            // Catch: string name = result;
            context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration,
                SyntaxKind.VariableDeclaration);

            // Catch: return result;
            context.RegisterSyntaxNodeAction(AnalyzeReturn,
                SyntaxKind.ReturnStatement);

            // Catch: SomeMethod(result)
            context.RegisterSyntaxNodeAction(AnalyzeArgument,
                SyntaxKind.Argument);
        }

        private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
        {
            var assignment = (AssignmentExpressionSyntax)context.Node;
            CheckExpression(context, assignment.Right);
        }

        private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
        {
            var declaration = (VariableDeclarationSyntax)context.Node;
            foreach (var variable in declaration.Variables)
            {
                if (variable.Initializer != null)
                {
                    CheckExpression(context, variable.Initializer.Value);
                }
            }
        }

        private static void AnalyzeReturn(SyntaxNodeAnalysisContext context)
        {
            var returnStatement = (ReturnStatementSyntax)context.Node;
            if (returnStatement.Expression != null)
            {
                CheckExpression(context, returnStatement.Expression);
            }
        }

        private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
        {
            var argument = (ArgumentSyntax)context.Node;
            CheckExpression(context, argument.Expression);
        }

        // ---------------------------------------------------------------
        //  Entry point: check expression and recurse into ternary branches
        // ---------------------------------------------------------------

        private static void CheckExpression(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression)
        {
            // First try the expression itself
            if (CheckImplicitConversion(context, expression))
                return;

            // If the expression is a ternary (target-typed conditional),
            // the compiler resolves the conversion per-branch, not on the whole expression.
            // We need to inspect WhenTrue and WhenFalse individually.
            if (expression is ConditionalExpressionSyntax conditional)
            {
                CheckExpression(context, conditional.WhenTrue);
                CheckExpression(context, conditional.WhenFalse);
                return;
            }

            // Parenthesized: (result)
            if (expression is ParenthesizedExpressionSyntax parens)
            {
                CheckExpression(context, parens.Expression);
            }
        }

        // ---------------------------------------------------------------
        //  Core: detect implicit conversion from Result<T> to T
        //  Returns true if a diagnostic was reported
        // ---------------------------------------------------------------

        private static bool CheckImplicitConversion(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax expression)
        {
            var conversion = context.SemanticModel.GetConversion(expression, context.CancellationToken);

            if (!conversion.IsImplicit || !conversion.IsUserDefined)
                return false;

            var sourceType = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type;
            if (!IsArdalisResultType(sourceType))
                return false;

            var namedType = sourceType as INamedTypeSymbol;
            if (namedType == null || !namedType.IsGenericType || namedType.TypeArguments.Length == 0)
                return false;

            var innerType = namedType.TypeArguments[0];

            var convertedType = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).ConvertedType;
            if (!SymbolEqualityComparer.Default.Equals(convertedType, innerType))
                return false;

            // Check if guarded
            var resultIdentifier = GetResultIdentifier(expression);
            if (resultIdentifier != null && IsGuarded(expression, resultIdentifier))
                return false;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, expression.GetLocation(),
                    expression.ToString(),
                    innerType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return true;
        }
    }
}
