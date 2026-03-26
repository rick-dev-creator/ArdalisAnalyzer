using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ArdalisAnalyzer.Analyzer
{
    internal static class ResultAnalyzerHelpers
    {
        // ---------------------------------------------------------------
        //  Type identification
        // ---------------------------------------------------------------

        internal static bool IsArdalisResultType(ITypeSymbol type)
        {
            if (type == null) return false;
            if (type.Name != "Result") return false;

            var ns = type.ContainingNamespace;
            if (ns == null || ns.Name != "Result") return false;
            ns = ns.ContainingNamespace;
            if (ns == null || ns.Name != "Ardalis") return false;
            return ns.ContainingNamespace?.IsGlobalNamespace == true;
        }

        // ---------------------------------------------------------------
        //  Identifier extraction
        // ---------------------------------------------------------------

        internal static string GetResultIdentifier(ExpressionSyntax expression)
        {
            if (expression is IdentifierNameSyntax id)
                return id.Identifier.Text;

            if (expression is MemberAccessExpressionSyntax ma)
                return ma.ToString();

            return null;
        }

        // ---------------------------------------------------------------
        //  Guard orchestration
        // ---------------------------------------------------------------

        internal static bool IsGuarded(SyntaxNode node, string resultIdentifier)
        {
            foreach (var ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case IfStatementSyntax ifStmt
                        when IsPositiveStatusCheck(ifStmt.Condition, resultIdentifier)
                          && ifStmt.Statement.Contains(node):
                        return true;

                    case SwitchSectionSyntax section
                        when section.Parent is SwitchStatementSyntax switchStmt
                          && IsStatusPropertyAccess(switchStmt.Expression, resultIdentifier)
                          && section.Labels.OfType<CaseSwitchLabelSyntax>()
                                 .Any(l => IsResultStatusOk(l.Value)):
                        return true;

                    case SwitchExpressionArmSyntax arm
                        when arm.Parent is SwitchExpressionSyntax switchExpr
                          && IsStatusPropertyAccess(switchExpr.GoverningExpression, resultIdentifier)
                          && arm.Pattern is ConstantPatternSyntax cp
                          && IsResultStatusOk(cp.Expression):
                        return true;

                    case ConditionalExpressionSyntax conditional
                        when conditional.WhenTrue.Contains(node)
                          && IsPositiveStatusCheck(conditional.Condition, resultIdentifier):
                        return true;

                    case BinaryExpressionSyntax binary
                        when binary.IsKind(SyntaxKind.LogicalAndExpression)
                          && binary.Right.Contains(node)
                          && IsPositiveStatusCheck(binary.Left, resultIdentifier):
                        return true;
                }
            }

            if (HasGuardClauseBefore(node, resultIdentifier))
                return true;

            return false;
        }

        // ---------------------------------------------------------------
        //  Guard clause — early exit before access
        // ---------------------------------------------------------------

        internal static bool HasGuardClauseBefore(SyntaxNode node, string resultIdentifier)
        {
            var current = node;
            while (current != null)
            {
                var block = current.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
                if (block == null) break;

                foreach (var statement in block.Statements)
                {
                    if (statement.SpanStart >= node.SpanStart)
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

        internal static bool IsPositiveStatusCheck(ExpressionSyntax condition, string id)
        {
            if (IsIsSuccessAccess(condition, id))
                return true;

            if (condition is BinaryExpressionSyntax binary)
            {
                if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsIsSuccessAccess(binary.Left, id) && IsTrueLiteral(binary.Right))
                        return true;
                    if (IsIsSuccessAccess(binary.Right, id) && IsTrueLiteral(binary.Left))
                        return true;

                    if (IsStatusPropertyAccess(binary.Left, id) && IsResultStatusOk(binary.Right))
                        return true;
                    if (IsStatusPropertyAccess(binary.Right, id) && IsResultStatusOk(binary.Left))
                        return true;
                }

                if (binary.IsKind(SyntaxKind.LogicalAndExpression))
                    return IsPositiveStatusCheck(binary.Left, id) ||
                           IsPositiveStatusCheck(binary.Right, id);
            }

            if (condition is ParenthesizedExpressionSyntax parens)
                return IsPositiveStatusCheck(parens.Expression, id);

            return false;
        }

        internal static bool IsNegativeStatusCheck(ExpressionSyntax condition, string id)
        {
            if (condition is PrefixUnaryExpressionSyntax prefix &&
                prefix.IsKind(SyntaxKind.LogicalNotExpression) &&
                IsIsSuccessAccess(prefix.Operand, id))
                return true;

            if (condition is BinaryExpressionSyntax binary)
            {
                if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsIsSuccessAccess(binary.Left, id) && IsFalseLiteral(binary.Right))
                        return true;
                    if (IsIsSuccessAccess(binary.Right, id) && IsFalseLiteral(binary.Left))
                        return true;
                }

                if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if (IsStatusPropertyAccess(binary.Left, id) && IsResultStatusOk(binary.Right))
                        return true;
                    if (IsStatusPropertyAccess(binary.Right, id) && IsResultStatusOk(binary.Left))
                        return true;
                }
            }

            if (condition is ParenthesizedExpressionSyntax parens)
                return IsNegativeStatusCheck(parens.Expression, id);

            return false;
        }

        // ---------------------------------------------------------------
        //  Leaf helpers
        // ---------------------------------------------------------------

        internal static bool IsIsSuccessAccess(ExpressionSyntax expr, string resultIdentifier)
        {
            return expr is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.Text == "IsSuccess" &&
                   GetResultIdentifier(ma.Expression) == resultIdentifier;
        }

        internal static bool IsStatusPropertyAccess(ExpressionSyntax expr, string resultIdentifier)
        {
            return expr is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.Text == "Status" &&
                   GetResultIdentifier(ma.Expression) == resultIdentifier;
        }

        internal static bool IsResultStatusOk(ExpressionSyntax expr)
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

        internal static bool IsEarlyExit(StatementSyntax statement)
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
