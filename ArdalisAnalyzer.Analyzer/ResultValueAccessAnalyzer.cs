using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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

            // Method call like GetUser().Value — always unguarded
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
        //  Type identification
        // ---------------------------------------------------------------

        private static bool IsArdalisResultType(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.Name != "Result") return false;

            // Walk the namespace chain: Result -> Ardalis.Result
            var ns = type.ContainingNamespace;
            if (ns == null || ns.Name != "Result") return false;
            ns = ns.ContainingNamespace;
            if (ns == null || ns.Name != "Ardalis") return false;
            // Must be directly under global namespace
            return ns.ContainingNamespace?.IsGlobalNamespace == true;
        }

        // ---------------------------------------------------------------
        //  Identifier extraction
        // ---------------------------------------------------------------

        private static string GetResultIdentifier(ExpressionSyntax expression)
        {
            if (expression is IdentifierNameSyntax id)
                return id.Identifier.Text;

            if (expression is MemberAccessExpressionSyntax ma)
                return ma.ToString();

            return null;
        }

        // ---------------------------------------------------------------
        //  Guard orchestration — flow analysis entry point
        // ---------------------------------------------------------------

        private static bool IsGuarded(
            MemberAccessExpressionSyntax valueAccess,
            string resultIdentifier)
        {
            // Single ancestor walk handles guards 1, 3, 4, 5, 6
            foreach (var ancestor in valueAccess.Ancestors())
            {
                switch (ancestor)
                {
                    // Guard 1 — if (result.IsSuccess) { ... result.Value ... }
                    case IfStatementSyntax ifStmt
                        when IsPositiveStatusCheck(ifStmt.Condition, resultIdentifier)
                          && ifStmt.Statement.Contains(valueAccess):
                        return true;

                    // Guard 3 — switch (result.Status) { case Ok: ... }
                    case SwitchSectionSyntax section
                        when section.Parent is SwitchStatementSyntax switchStmt
                          && IsStatusPropertyAccess(switchStmt.Expression, resultIdentifier)
                          && section.Labels.OfType<CaseSwitchLabelSyntax>()
                                 .Any(l => IsResultStatusOk(l.Value)):
                        return true;

                    // Guard 4 — result.Status switch { Ok => result.Value }
                    case SwitchExpressionArmSyntax arm
                        when arm.Parent is SwitchExpressionSyntax switchExpr
                          && IsStatusPropertyAccess(switchExpr.GoverningExpression, resultIdentifier)
                          && arm.Pattern is ConstantPatternSyntax cp
                          && IsResultStatusOk(cp.Expression):
                        return true;

                    // Guard 5 — result.IsSuccess ? result.Value : fallback
                    case ConditionalExpressionSyntax conditional
                        when conditional.WhenTrue.Contains(valueAccess)
                          && IsPositiveStatusCheck(conditional.Condition, resultIdentifier):
                        return true;

                    // Guard 6 — result.IsSuccess && result.Value.Length > 0
                    case BinaryExpressionSyntax binary
                        when binary.IsKind(SyntaxKind.LogicalAndExpression)
                          && binary.Right.Contains(valueAccess)
                          && IsPositiveStatusCheck(binary.Left, resultIdentifier):
                        return true;
                }
            }

            // Guard 2 — if (!result.IsSuccess) return/throw; ... result.Value
            if (HasGuardClauseBefore(valueAccess, resultIdentifier))
                return true;

            return false;
        }

        // ---------------------------------------------------------------
        //  Guard 2 — Guard clause (early exit) before Value access
        //  if (!result.IsSuccess) return;
        //  if (!result.IsSuccess) throw new ...;
        //  if (result.Status != ResultStatus.Ok) return;
        // ---------------------------------------------------------------

        private static bool HasGuardClauseBefore(
            MemberAccessExpressionSyntax valueAccess,
            string resultIdentifier)
        {
            var current = (SyntaxNode)valueAccess;
            while (current != null)
            {
                var block = current.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
                if (block == null) break;

                foreach (var statement in block.Statements)
                {
                    if (statement.SpanStart >= valueAccess.SpanStart)
                        break;

                    if (statement is IfStatementSyntax ifStmt &&
                        IsNegativeStatusCheck(ifStmt.Condition, resultIdentifier) &&
                        IsEarlyExit(ifStmt.Statement))
                    {
                        return true;
                    }
                }

                current = block;
            }

            return false;
        }

        // ---------------------------------------------------------------
        //  Condition helpers
        // ---------------------------------------------------------------

        private static bool IsPositiveStatusCheck(ExpressionSyntax condition, string id)
        {
            // result.IsSuccess
            if (IsIsSuccessAccess(condition, id))
                return true;

            if (condition is BinaryExpressionSyntax binary)
            {
                // result.IsSuccess == true
                if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsIsSuccessAccess(binary.Left, id) && IsTrueLiteral(binary.Right))
                        return true;
                    if (IsIsSuccessAccess(binary.Right, id) && IsTrueLiteral(binary.Left))
                        return true;

                    // result.Status == ResultStatus.Ok
                    if (IsStatusPropertyAccess(binary.Left, id) && IsResultStatusOk(binary.Right))
                        return true;
                    if (IsStatusPropertyAccess(binary.Right, id) && IsResultStatusOk(binary.Left))
                        return true;
                }

                // result.IsSuccess && otherCondition
                if (binary.IsKind(SyntaxKind.LogicalAndExpression))
                    return IsPositiveStatusCheck(binary.Left, id) ||
                           IsPositiveStatusCheck(binary.Right, id);
            }

            // Parenthesized: (result.IsSuccess)
            if (condition is ParenthesizedExpressionSyntax parens)
                return IsPositiveStatusCheck(parens.Expression, id);

            return false;
        }

        private static bool IsNegativeStatusCheck(ExpressionSyntax condition, string id)
        {
            // !result.IsSuccess
            if (condition is PrefixUnaryExpressionSyntax prefix &&
                prefix.IsKind(SyntaxKind.LogicalNotExpression) &&
                IsIsSuccessAccess(prefix.Operand, id))
                return true;

            if (condition is BinaryExpressionSyntax binary)
            {
                // result.IsSuccess == false
                if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsIsSuccessAccess(binary.Left, id) && IsFalseLiteral(binary.Right))
                        return true;
                    if (IsIsSuccessAccess(binary.Right, id) && IsFalseLiteral(binary.Left))
                        return true;
                }

                // result.Status != ResultStatus.Ok
                if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if (IsStatusPropertyAccess(binary.Left, id) && IsResultStatusOk(binary.Right))
                        return true;
                    if (IsStatusPropertyAccess(binary.Right, id) && IsResultStatusOk(binary.Left))
                        return true;
                }
            }

            // Parenthesized
            if (condition is ParenthesizedExpressionSyntax parens)
                return IsNegativeStatusCheck(parens.Expression, id);

            return false;
        }

        // ---------------------------------------------------------------
        //  Leaf helpers
        // ---------------------------------------------------------------

        private static bool IsIsSuccessAccess(ExpressionSyntax expr, string resultIdentifier)
        {
            return expr is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.Text == "IsSuccess" &&
                   GetResultIdentifier(ma.Expression) == resultIdentifier;
        }

        private static bool IsStatusPropertyAccess(ExpressionSyntax expr, string resultIdentifier)
        {
            return expr is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.Text == "Status" &&
                   GetResultIdentifier(ma.Expression) == resultIdentifier;
        }

        private static bool IsResultStatusOk(ExpressionSyntax expr)
        {
            return expr is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.Text == "Ok";
        }

        private static bool IsTrueLiteral(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit &&
                   lit.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        private static bool IsFalseLiteral(ExpressionSyntax expr)
        {
            return expr is LiteralExpressionSyntax lit &&
                   lit.IsKind(SyntaxKind.FalseLiteralExpression);
        }

        private static bool IsEarlyExit(StatementSyntax statement)
        {
            if (statement is ReturnStatementSyntax || statement is ThrowStatementSyntax)
                return true;

            if (statement is BlockSyntax block)
            {
                var last = block.Statements.LastOrDefault();
                return last is ReturnStatementSyntax || last is ThrowStatementSyntax;
            }

            return false;
        }
    }
}
