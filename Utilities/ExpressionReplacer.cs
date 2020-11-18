using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public class ExpressionReplacer : ExpressionVisitor
    {
        private readonly Func<Expression, Expression> replacer;

        public ExpressionReplacer(Func<Expression, Expression> replacer)
        {
            this.replacer = replacer;
        }

        public override Expression Visit(Expression node)
        {
            return base.Visit(replacer(node));
        }
    }
}
