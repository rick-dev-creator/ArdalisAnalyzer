using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static ArdalisAnalyzer.Analyzer.ResultAnalyzerHelpers;

namespace ArdalisAnalyzer.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ResultValueAccessAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ARDRES001";

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Result.Value accessed without checking status",
            messageFormat: "'{0}.Value' is accessed without verifying the result status (IsSuccess/Status) first",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Accessing Result.Value without first checking IsSuccess or Status can lead to NullReferenceException or silent data corruption.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // result.Value
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

            // result?.Value
            context.RegisterSyntaxNodeAction(AnalyzeConditionalAccess, SyntaxKind.ConditionalAccessExpression);
        }

        // ---------------------------------------------------------------
        //  result.Value
        // ---------------------------------------------------------------

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccess = (MemberAccessExpressionSyntax)context.Node;

            if (memberAccess.Name.Identifier.Text != "Value")
                return;

            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
            if (!IsArdalisResultType(typeInfo.Type))
                return;

            var resultIdentifier = GetResultIdentifier(memberAccess.Expression);

            if (resultIdentifier == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.Expression));
                return;
            }

            if (!IsGuarded(memberAccess, resultIdentifier))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, memberAccess.GetLocation(), resultIdentifier));
            }
        }

        // ---------------------------------------------------------------
        //  result?.Value  /  result?.Value?.Length  /  result?.Value ?? x
        // ---------------------------------------------------------------

        private static void AnalyzeConditionalAccess(SyntaxNodeAnalysisContext context)
        {
            var conditionalAccess = (ConditionalAccessExpressionSyntax)context.Node;

            // Get the innermost WhenNotNull — could be .Value or .Value?.Length
            var whenNotNull = conditionalAccess.WhenNotNull;

            // Check if .Value is the first member binding: result?.Value
            MemberBindingExpressionSyntax valueBinding = null;

            if (whenNotNull is MemberBindingExpressionSyntax binding)
            {
                valueBinding = binding;
            }
            // result?.Value?.Length or result?.Value.Length
            else if (whenNotNull is ConditionalAccessExpressionSyntax nested &&
                     nested.Expression is MemberBindingExpressionSyntax nestedBinding)
            {
                valueBinding = nestedBinding;
            }
            else if (whenNotNull is MemberAccessExpressionSyntax ma &&
                     ma.Expression is MemberBindingExpressionSyntax maBinding)
            {
                valueBinding = maBinding;
            }
            // result?.Value ?? "default"  — the ?? wraps the conditional access at a higher level,
            // so the WhenNotNull is still a MemberBindingExpression (handled above)

            if (valueBinding == null || valueBinding.Name.Identifier.Text != "Value")
                return;

            // Check if the expression (left of ?.) is a Result type
            var typeInfo = context.SemanticModel.GetTypeInfo(conditionalAccess.Expression, context.CancellationToken);
            if (!IsArdalisResultType(typeInfo.Type))
                return;

            var resultIdentifier = GetResultIdentifier(conditionalAccess.Expression);

            if (resultIdentifier == null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, conditionalAccess.GetLocation(), conditionalAccess.Expression));
                return;
            }

            if (!IsGuarded(conditionalAccess, resultIdentifier))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, conditionalAccess.GetLocation(), resultIdentifier));
            }
        }
    }
}
