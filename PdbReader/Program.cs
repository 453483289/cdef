﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dia2Lib;
using PdbReader.Collect;
using PdbReader.Types;
using PdbReader.Defs;
using PdbReader.Xml;

namespace PdbReader
{
    class Program
    {
        const string DOT = "....";
        const string TAB = "    ";
        static void TypesTest()
        {
            CType t1 = new CPrim("int");
            CType t2 = new CPrim("float");
            // CType t2 = new CPtr(t);
            // CType t3 = new CPtr(new CArr(t, 10));

            //CEnum e1 = new CEnum();
            //e1.Add("RED", 0);
            //e1.Add("GREEN", 1);
            //e1.Add("BLUE", 2);

            CStruct s1 = new CStruct();
            s1.Add(t1, "a");

            CFunc f2 = new CFunc(t1);
            CFunc f1 = new CFunc(new CPtr(f2));
            f1.Add(new CPtr(new CPtr(t1)));

            s1.Add(new CArr(new CPtr(f1), 10), "call");

            s1.Add(new CTypeRef("LIST_ENTRY"), "first");
            s1.Add(t2, "b");

            Console.Write(new DefFactory().CreateMixedTypedef(s1, "ENTRY").Output("....", TAB));
        }
        static void TypesTest2()
        {
            SortedSet<TypeAttr> attrs = new SortedSet<TypeAttr>();
            // attrs.Add(TypeAttrs.Volatile);
            attrs.Add(TypeAttrs.Const);

            CStruct s1 = new CStruct();
            s1.Add(PrimTypes.INT, "a");
            s1.Add(PrimTypes.INT, "b");

            CType t1 = new CAttrTerm(s1, attrs);
            Console.WriteLine(t1.Define("v", "....", "    "));
        }
        static void XmlSerializeTest()
        {
            XmlMaker x = new XmlMaker();
            CStruct struc = new CStruct();
            struc.Add(new CArr(PrimTypes.INT, 10), "I");
            struc.Add(new CPtr(PrimTypes.VOID), "am");
            struc.Add(PrimTypes.DOUBLE, "a");

            CFunc func = new CFunc(PrimTypes.VOID);
            func.Add(new CPtr(PrimTypes.VOID));
            struc.Add(new CPtr(func), "func");

            x.AddNamed(struc, "What");
            x.WriteResultTo(Console.OpenStandardOutput());
        }
        static void Main(string[] args)
        {
            XmlSerializeTest();
            Environment.Exit(0);
            // const string filePath = @"E:\DebuggingSymbols\ntdll.pdb\DDC94C54F06040619595D2473D92AB911\ntdll.pdb";
            // const string filePath = @"F:\GuBigCollect\Tests_PDB\T10_PR_01\Debug\T10_PR_01.pdb";
            const string filePath = @"F:\ntkrnlmp.pdb";
            IDiaDataSource source = new DiaSource();
            IDiaSession session;
            source.loadDataFromPdb(filePath);
            source.openSession(out session);

            IDiaSymbol global = session.globalScope;
            IDiaEnumSymbols enumSymbols;
            global.findChildren(SymTagEnum.SymTagUDT, "_PEB", 0, out enumSymbols);

            IDiaSymbol struct1 = enumSymbols.Item(0);
            struct1.findChildren(SymTagEnum.SymTagData, null, 0, out enumSymbols);

            IDiaSymbol member1 = enumSymbols.Item(0);

            Translator t = new Translator();
            CType t2 = t.TranslateStruct(struct1);

            Console.Write(new DefFactory().CreatePureDef((CBrace)t2, "X").Output(DOT, TAB));
            
        }
    }
}
