﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RMUD
{
    public partial class CommandFactory
    {
        public static CommandTokenMatcher Optional(CommandTokenMatcher Sub)
        {
            return new Optional(Sub);
        }
    }

    internal class Optional : CommandTokenMatcher
    {
        public CommandTokenMatcher Sub;

        public Optional(CommandTokenMatcher Sub)
        {
            this.Sub = Sub;
        }

        public List<PossibleMatch> Match(PossibleMatch State, MatchContext Context)
        {
            var R = new List<PossibleMatch>();
            R.AddRange(Sub.Match(State, Context));
            if (R.Count == 0) R.Add(State);
            return R;
        }

        public String FindFirstKeyWord() { return Sub.FindFirstKeyWord(); }
        public String Emit() { return Sub.Emit() + "?"; }
    }
}
