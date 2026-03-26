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
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

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
    }
}
