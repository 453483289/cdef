﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dia2Lib;
using PdbReader.DiaExtra;
using PdbReader.Types;
using PdbReader.Collect;

namespace PdbReader
{
    class Translator
    {
        public CType Translate(IDiaSymbol sym)
        {
            switch ((SymTagEnum)sym.symTag)
            {
                case SymTagEnum.SymTagBaseType:
                    return TranslateBaseType(sym);

                case SymTagEnum.SymTagPointerType:
                    return TranslatePtr(sym);

                case SymTagEnum.SymTagArrayType:
                    return TranslateArr(sym);

                case SymTagEnum.SymTagFunctionType:
                    return TranslateFunc(sym);
                
                case SymTagEnum.SymTagUDT:
                    return PdbSymbol.IsUnnamed(sym)
                        ? TranslateUnnamedUdt(sym)
                        : TranslateTypeRef(sym);

                case SymTagEnum.SymTagEnum:
                    return PdbSymbol.IsUnnamed(sym)
                        ? TranslateEnum(sym)
                        : TranslateTypeRef(sym);

                default:
                    throw new NotImplementedException(((SymTagEnum)sym.symTag).ToString());
            }
        }
        private CTerm WithAttr(CTerm type, IDiaSymbol sym)
        {
            SortedSet<TypeAttr> attrs = new SortedSet<TypeAttr>();
            if (sym.constType == 1) { attrs.Add(TypeAttrs.Const); }
            if (sym.volatileType == 1) { attrs.Add(TypeAttrs.Volatile); }
            if (sym.unalignedType == 1) { attrs.Add(TypeAttrs.Unaligned); }

            return attrs.Any() ? new CAttrTerm(type, attrs) : type;
        }
        public CPrefix _TranslateBaseType(IDiaSymbol sym)
        {
            int size = (int)sym.length;
            switch ((BaseTypeEnum)sym.baseType)
            {
                case BaseTypeEnum.btVoid:
                    return PrimTypes.VOID;

                case BaseTypeEnum.btChar:
                    return PrimTypes.CHAR;
                case BaseTypeEnum.btWChar:
                    return PrimTypes.WCHAR;

                case BaseTypeEnum.btInt:
                    return IntTypePairs.SelectBySize(size).Signed;
                case BaseTypeEnum.btUInt:
                    return IntTypePairs.SelectBySize(size).Unsigned;

                // the design logic of Dia2Lib is
                // eh.. MS guys must be brain fucked at that time.
                case BaseTypeEnum.btLong:
                    return PrimTypes.LONG;
                case BaseTypeEnum.btULong:
                    return PrimTypes.ULONG;

                case BaseTypeEnum.btFloat:
                    return sym.length == 4 ? PrimTypes.FLOAT : PrimTypes.FLOAT;

                case BaseTypeEnum.btHresult:
                    return new CTypeRef("HRESULT");

                default:
                    throw new NotImplementedException(((BaseTypeEnum)sym.baseType).ToString());
            }
        }
        public CTerm TranslateBaseType(IDiaSymbol sym)
        {
            return WithAttr(_TranslateBaseType(sym), sym);
        }
        public CPtr TranslatePtr(IDiaSymbol sym)
        {
            CType next = Translate(sym.type);
            return new CPtr(next);
        }
        public CArr TranslateArr(IDiaSymbol sym)
        {
            CType next = Translate(sym.type);
            int len = (int)sym.count;           // it should be safe
            return new CArr(next, len);
        }
        private CallConv TranslateCallConv(uint callConv)
        {
            switch ((CvCallEnum)callConv)
            {
                case CvCallEnum.NEAR_C:
                    return CallConvs.Default;
                case CvCallEnum.NEAR_STD:
                    return CallConvs.Stdcall;
                default:
                    throw new NotImplementedException(((CvCallEnum)callConv).ToString());
            }
        }
        public CFunc TranslateFunc(IDiaSymbol sym)
        {
            CType retType = Translate(sym.type);
            CFunc res = new CFunc(retType, TranslateCallConv(sym.callingConvention));

            IDiaEnumSymbols syms;
            sym.findChildren(SymTagEnum.SymTagFunctionArgType, null, 0, out syms);

            if (syms.count == 0)
            {
                res.Add(PrimTypes.VOID);
                return res;
            }
            else if (syms.count == 1)
            {
                IDiaSymbol only = syms.Item(0).type;
                if ((SymTagEnum)only.symTag == SymTagEnum.SymTagBaseType &&
                    (BaseTypeEnum)only.baseType == BaseTypeEnum.btNoType)
                {
                    return res;
                }
            }

            foreach (IDiaSymbol argSym in syms)
            {
                CType argType = Translate(argSym.type);
                res.Add(argType);
            }
            return res;
        }
        public CBits TranslateBitField(IDiaSymbol sym)
        {
            return new CBits(TranslateBaseType(sym.type), (int)sym.length);
        }
        private bool IsBitField(IDiaSymbol sym)
        {
            return (LocationTypeEnum)sym.locationType == LocationTypeEnum.LocIsBitField;
        }
        public CType TranslateMember(IDiaSymbol subSym)
        {
            return IsBitField(subSym)
                ? TranslateBitField(subSym)
                : Translate(subSym.type);
        }
        public CStruct TranslateStruct(IDiaSymbol sym)
        {
            IDiaEnumSymbols symbols;
            sym.findChildren(SymTagEnum.SymTagData, null, 0, out symbols);

            CStruct res = new CStruct();
            Offset lastOffset = Offset.Neg;
            foreach (IDiaSymbol subSym in symbols)
            {
                Offset thisOffset = Offset.FromDiaSymbol(subSym);
                if (thisOffset.IsLessThanOrEqualTo(lastOffset))
                {
                    symbols.Reset();
                    return TranslateStruct2(symbols);
                }

                string name = subSym.name;
                CType type = TranslateMember(subSym);
                res.Add(type, name, thisOffset);

                lastOffset = thisOffset;
            }

            return res;
        }
        public CStruct TranslateStruct2(IDiaEnumSymbols symbols)
        {
            return new Collector(this).CollectStruct(symbols);
        }
        public CUnion TranslateUnion(IDiaSymbol sym)
        {
            IDiaEnumSymbols symbols;
            sym.findChildren(SymTagEnum.SymTagData, null, 0, out symbols);

            CUnion res = new CUnion();
            foreach (IDiaSymbol subSym in symbols)
            {
                Offset thisOffset = Offset.FromDiaSymbol(subSym);
                if (!thisOffset.IsEqualTo(Offset.Zero))
                {
                    symbols.Reset();
                    return TranslateUnion2(symbols);
                }

                string name = subSym.name;
                CType type = TranslateMember(subSym);
                res.Add(type, name);
            }
            return res;
        }
        public CUnion TranslateUnion2(IDiaEnumSymbols symbols)
        {
            return new Collector(this).CollectUnion(symbols);
        }
        public CType TranslateTypeRef(IDiaSymbol sym)
        {
            return new CTypeRef(PdbSymbol.InternName(sym.name));
        }
        public CType TranslateUnnamedUdt(IDiaSymbol sym)
        {
            switch ((UdtKindEnum)sym.udtKind)
            {
                case UdtKindEnum.UdtStruct:
                    return TranslateStruct(sym);
                case UdtKindEnum.UdtUnion:
                    return TranslateUnion(sym);
                default:
                    return new CPrim("NotImpl_Udt");
            }
        }
        public CEnum TranslateEnum(IDiaSymbol sym)
        {
            IDiaEnumSymbols symbols;
            sym.findChildren(SymTagEnum.SymTagNull, null, 0, out symbols);

            CEnum res = new CEnum();
            foreach (IDiaSymbol constant in symbols)
            {
                res.Add(constant.name, (uint)constant.value);
            }
            return res;
        }
    }
}
