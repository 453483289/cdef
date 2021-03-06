﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdbReader.Types
{
    class Decorator : IWrapVisitor
    {
        private string _s;
        private bool _lastIsPtr;

        public Decorator(string s)
        {
            _s = s;
            _lastIsPtr = false;
        }

        public string Result
        {
            get { return _s; }
        }

        public void AfterVisit(CWrap type)
        {
            _lastIsPtr = type is CPtr;
        }
        private string MaybeParen(string s)
        {
            return _lastIsPtr ? "(" + s + ")" : s;
        }
        private string WithCallConv(string s, CallConv conv)
        {
            return
                object.ReferenceEquals(conv, CallConvs.Default)
                ? s
                : conv.Keyword + " " + s;
        }

        public void VisitArr(CArr arr)
        {
            _s = MaybeParen(_s) + "[" + arr.Len + "]";
        }

        public void VisitPtr(CPtr ptr)
        {
            _s = "*" + _s;
        }

        private string FuncArgsStr(CFunc func)
        {
            List<CType> args = func.Args;
            return args.Any()
                ? string.Join(", ", args.Select(a => a.Sig))
                : "void";
        }
        public void VisitFunc(CFunc func)
        {
            _s = MaybeParen(WithCallConv(_s, func.CallConv))
                + "(" + FuncArgsStr(func) + ")";
        }

        public void VisitBits(CBits bits)
        {
            _s = _s + " : " + bits.Len;
        }
    }
}
