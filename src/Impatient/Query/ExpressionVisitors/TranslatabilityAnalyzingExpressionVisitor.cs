﻿using Impatient.Query.Expressions;
using System.Linq;
using System.Linq.Expressions;

namespace Impatient.Query.ExpressionVisitors
{
    public class TranslatabilityAnalyzingExpressionVisitor : ExpressionVisitor
    {
        public TranslatabilityAnalyzingExpressionVisitor()
        {
        }

        public virtual bool ComplexNestedQueriesSupported => true;

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (Visit(node.Left) is TranslatableExpression t1
                && Visit(node.Right) is TranslatableExpression t2)
            {
                switch (node.NodeType)
                {
                    // Equality
                    case ExpressionType.Equal:
                    case ExpressionType.NotEqual:
                    {
                        return new TranslatableExpression(node);
                    }

                    // Comparison
                    case ExpressionType.GreaterThan:
                    case ExpressionType.GreaterThanOrEqual:
                    case ExpressionType.LessThan:
                    case ExpressionType.LessThanOrEqual:
                    // Logical
                    case ExpressionType.AndAlso:
                    case ExpressionType.OrElse:
                    // Math
                    case ExpressionType.Add:
                    case ExpressionType.Subtract:
                    case ExpressionType.Multiply:
                    case ExpressionType.Divide:
                    case ExpressionType.Modulo:
                    // Bitwise
                    case ExpressionType.And:
                    case ExpressionType.Or:
                    case ExpressionType.ExclusiveOr:
                    // Other
                    case ExpressionType.Coalesce:
                    {
                        if (t1.Type.IsScalarType() && t2.Type.IsScalarType())
                        {
                            return new TranslatableExpression(node);
                        }

                        break;
                    }
                }
            }

            return node;
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (Visit(node.Test) is TranslatableExpression
                && Visit(node.IfTrue) is TranslatableExpression t1
                && Visit(node.IfFalse) is TranslatableExpression t2
                && t1.Type.IsScalarType()
                && t2.Type.IsScalarType())
            {
                return new TranslatableExpression(node);
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type.IsScalarType())
            {
                return new TranslatableExpression(node);
            }

            return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case SqlExpression sqlExpression:
                {
                    return new TranslatableExpression(node);
                }

                case PolymorphicExpression polymorphicExpression:
                {
                    return new TranslatableExpression(node);
                }

                case GroupByResultExpression groupByResultExpression
                when ComplexNestedQueriesSupported:
                {
                    return new TranslatableExpression(node);
                }

                case GroupedRelationalQueryExpression groupedRelationalQueryExpression
                when ComplexNestedQueriesSupported:
                {
                    return new TranslatableExpression(node);
                }

                case SingleValueRelationalQueryExpression singleValueRelationalQueryExpression
                when singleValueRelationalQueryExpression.Type.IsScalarType() || ComplexNestedQueriesSupported:
                {
                    return new TranslatableExpression(node);
                }

                case EnumerableRelationalQueryExpression enumerableRelationalQueryExpression
                when ComplexNestedQueriesSupported:
                {
                    return new TranslatableExpression(node);
                }

                case AnnotationExpression annotationExpression:
                {
                    return Visit(annotationExpression.Expression);
                }
            }

            return node;
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if (node.Members == null)
            {
                if (node.Arguments.Count == 0)
                {
                    return new TranslatableExpression(node);
                }
            }
            else if (node.Arguments.All(a => Visit(a) is TranslatableExpression))
            {
                return new TranslatableExpression(node);
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // TODO: Can this be replaced with checking for 'IsParameterizable' like in the translator?

            if (node.Type.IsScalarType())
            {
                var expression = node.Expression;

                while (expression is MemberExpression memberExpression)
                {
                    expression = memberExpression.Expression;
                }

                if (expression is ParameterExpression)
                {
                    return new TranslatableExpression(node);
                }
            }

            return node;
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (Visit(node.NewExpression) is TranslatableExpression
                && node.Bindings.All(IsTranslatable))
            {
                return new TranslatableExpression(node);
            }

            return node;
        }

        private bool IsTranslatable(MemberBinding memberBinding)
        {
            switch (memberBinding)
            {
                case MemberAssignment memberAssignment:
                {
                    return Visit(memberAssignment.Expression) is TranslatableExpression;
                }

                case MemberMemberBinding memberMemberBinding:
                {
                    return memberMemberBinding.Bindings.All(IsTranslatable);
                }

                default:
                {
                    return false;
                }
            }
        }

        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.TypeEqual:
                case ExpressionType.TypeIs:
                {
                    if (Visit(node.Expression) is TranslatableExpression
                        && node.Expression is PolymorphicExpression)
                    {
                        return new TranslatableExpression(node);
                    }

                    goto default;
                }

                default:
                {
                    return node;
                }
            }
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Not:
                case ExpressionType.Convert:
                {
                    if (Visit(node.Operand) is TranslatableExpression)
                    {
                        return new TranslatableExpression(node);
                    }

                    goto default;
                }

                default:
                {
                    return node;
                }
            }
        }
    }
}
