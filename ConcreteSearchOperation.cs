using Blast.Core.Results;

namespace GooDic.Fluent.Plugin
{
    public class ConcreteSearchOperation : SearchOperationBase
    {
        public ConcreteSearchOperation() : base(
            "Query entered word or sentence",
            "Open goo dictionary",
            "\uF6FA"
        )
        { }
    }
}
