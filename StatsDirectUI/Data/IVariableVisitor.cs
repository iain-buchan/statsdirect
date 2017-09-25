using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Data
{
    public interface IVariableVisitor
    {
        void Visit(BooleanVariable variable);
        void Visit(ClassifierVariable variable);
        void Visit(DateVariable variable);
        void Visit(DoubleVariable variable);
        void Visit(StringVariable variable);
        void Visit(VariantVariable variable);
    }
}
