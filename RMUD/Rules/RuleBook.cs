﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RMUD
{
    public class RuleBook
    {
        public String Name;
        public String Description;
        public List<Type> ArgumentTypes = new List<Type>();
        public Type ResultType;

        public bool CheckArgumentTypes(Type ResultType, params Type[] ArgTypes)
        {
            if (this.ResultType != ResultType) return false;
            if (ArgTypes.Length != ArgumentTypes.Count) return false;
            for (int i = 0; i < ArgTypes.Length; ++i)
                if (!ArgumentTypes[i].IsAssignableFrom(ArgTypes[i]))
                    return false;
            return true;
        }

        public virtual void AddRule(Rule Rule) { throw new NotImplementedException(); }
    }

    public class ActionRuleBook : RuleBook
    {
        public List<Rule<RuleResult>> Rules = new List<Rule<RuleResult>>();

        public ActionRuleBook()
        {
            ResultType = typeof(RuleResult);
        }

        public RuleResult Consider(params Object[] Args)
        {
            foreach (var rule in Rules)
            {
                if (rule.WhenClause == null || rule.WhenClause.Invoke(Args))
                {
                    var r = rule.BodyClause == null ? RuleResult.Default : rule.BodyClause.Invoke(Args);
                    if (r != RuleResult.Continue) return r;
                }
            }
            return RuleResult.Default;
        }

        public override void AddRule(Rule Rule)
        {
            if (!(Rule is Rule<RuleResult>)) throw new InvalidOperationException();
            Rules.Add(Rule as Rule<RuleResult>);
        }
    }

    public class ValueRuleBook<RT> : RuleBook
    {
        public List<Rule<RT>> Rules = new List<Rule<RT>>();

        public ValueRuleBook()
        {
            ResultType = typeof(RT);
        }

        public RT Consider(out bool ValueReturned, params Object[] Args)
        {
            ValueReturned = false;
            foreach (var rule in Rules)
                if (rule.WhenClause == null || rule.WhenClause.Invoke(Args))
                {
                    ValueReturned = true;
                    return rule.BodyClause.Invoke(Args);
                }
            return default(RT);
        }

        public override void AddRule(Rule Rule)
        {
            if (!(Rule is Rule<RT>)) throw new InvalidOperationException();
            Rules.Add(Rule as Rule<RT>);
        }
    }
}