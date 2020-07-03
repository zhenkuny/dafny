//-----------------------------------------------------------------------------
//
// Copyright (C) Amazon.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny {
  public class CppCompiler : Compiler {
    public CppCompiler(ErrorReporter reporter, ReadOnlyCollection<string> otherHeaders)
    : base(reporter) {
      this.headers = otherHeaders;
      this.datatypeDecls = new List<DatatypeDecl>();
      this.classDefaults = new List<string>();
    }

    private ReadOnlyCollection<string> headers;
    private List<DatatypeDecl> datatypeDecls;
    private List<string> classDefaults;
    
    /*
     * Unlike other Dafny and Dafny's other backends, C++ cares about
     * the order in which types are declared.  To make this more likely
     * to succeed, we emit type information as gradually as possible
     * in hopes that definitions are in place when needed.
     */

    // Forward declarations of class and struct names
    private TargetWriter modDeclsWr = null;
    private TargetWriter modDeclWr = null;
    // Dafny datatype declarations
    private TargetWriter dtDeclsWr = null;
    private TargetWriter dtDeclWr = null;
    // Dafny class declarations
    private TargetWriter classDeclsWr = null;
    private TargetWriter classDeclWr = null;
    // Dedicated hash-function definitions for each type
    private TargetWriter hashWr = null;

    // Shadowing variables in Compiler.cs
    new string DafnySetClass = "DafnySet";
    new string DafnyMultiSetClass = "DafnyMultiset";
    new string DafnySeqClass = "DafnySequence";
    new string DafnyMapClass = "DafnyMap";

    public override string TargetLanguage => "Cpp";
    protected override string ModuleSeparator => "::";
    protected override string ClassAccessor => "->";

    protected override void EmitHeader(Program program, TargetWriter wr) {
      wr.WriteLine("// Dafny program {0} compiled into Cpp", program.Name);
      wr.WriteLine("#include \"DafnyRuntime.h\"");
      foreach (var header in this.headers) {
        wr.WriteLine("#include \"{0}\"", header);
      }

      var filenameNoExtension = program.Name.Substring(0, program.Name.Length - 4);
      var headerFileName = String.Format("{0}.h", filenameNoExtension);
      wr.WriteLine("#include \"{0}\"", headerFileName);

      var headerFileWr = wr.NewFile(headerFileName);
      headerFileWr.WriteLine("// Dafny program {0} compiled into a Cpp header file", program.Name);
      headerFileWr.WriteLine("#pragma once");
      headerFileWr.WriteLine("#include \"DafnyRuntime.h\"");

      this.modDeclsWr = headerFileWr.ForkSection();
      this.dtDeclsWr = headerFileWr.ForkSection();
      this.classDeclsWr = headerFileWr.ForkSection();
      this.hashWr = headerFileWr.ForkSection();
    }

    protected override void EmitFooter(Program program, TargetWriter wr) {
      // Define default values for each datatype
      foreach (var dt in this.datatypeDecls) {
        var wd = wr.NewBlock(String.Format("template <{0}>\nstruct get_default<{1}::{2}{3} >",
          TypeParameters(dt.TypeArgs),
          dt.Module.CompileName,
          dt.CompileName,
          InstantiateTemplate(dt.TypeArgs)), ";");
        var wc = wd.NewBlock(String.Format("static {0}::{1}{2} call()",
          dt.Module.CompileName,
          dt.CompileName,
          InstantiateTemplate(dt.TypeArgs)));
        wc.WriteLine("return {0}::{1}{2}();", dt.Module.CompileName, dt.CompileName, InstantiateTemplate(dt.TypeArgs));
      }

      // Define default values for each class
      foreach (var classDefault in classDefaults) {
        wr.WriteLine(classDefault);
      }
    }

    public override void EmitCallToMain(Method mainMethod, TargetWriter wr) {
      var w = wr.NewBlock("int main()");
      var tryWr = w.NewBlock("try");
      tryWr.WriteLine(string.Format("{0}::{1}::{2}();", mainMethod.EnclosingClass.Module.CompileName, mainMethod.EnclosingClass.CompileName, mainMethod.Name));
      var catchWr = w.NewBlock("catch (DafnyHaltException & e)");
      catchWr.WriteLine("std::cout << \"Program halted: \" << e.what() << std::endl;");
    }

    protected override BlockTargetWriter CreateStaticMain(IClassWriter cw) {
      var wr = (cw as CppCompiler.ClassWriter).MethodWriter;
      return wr.NewBlock("int main()");
    }

    protected override TargetWriter CreateModule(string moduleName, bool isDefault, bool isExtern, string/*?*/ libraryName, TargetWriter wr) {
      var s = string.Format("namespace {0} ", IdProtect(moduleName));
      this.modDeclWr = this.modDeclsWr.NewBigBlock(s, "// end of " + s + " declarations");
      this.dtDeclWr = this.dtDeclsWr.NewBigBlock(s, "// end of " + s + " datatype declarations");
      this.classDeclWr = this.classDeclsWr.NewBigBlock(s, "// end of " + s + " class declarations");
      return wr.NewBigBlock(s, "// end of " + s);
    }

    private string TypeParameters(List<TypeParameter> targs) {
      Contract.Requires(cce.NonNullElements(targs));
      Contract.Ensures(Contract.Result<string>() != null);
      if (targs != null) {
        return Util.Comma(targs, tp => "typename " + IdName(tp));
      } else {
        return "";
      }
    }

    private string DeclareTemplate(List<TypeParameter> typeArgs) {
      var targs = "";
      if (typeArgs != null && typeArgs.Count > 0) {
        targs = String.Format("template <{0}>", TypeParameters(typeArgs));
      }
      return targs;
    }

    private string DeclareTemplate(List<Type> typeArgs) {
      var targs = "";
      if (typeArgs != null && typeArgs.Count > 0) {
        targs = String.Format("template <{0}>", Util.Comma(typeArgs, t => "typename " + TypeName(t, null, null)));
      }
      return targs;
    }

    private string InstantiateTemplate(List<TypeParameter> typeArgs) {
      if (typeArgs != null) {
        var targs = "";
        if (typeArgs.Count > 0) {
          targs = String.Format("<{0}>", Util.Comma(typeArgs, ta => ta.CompileName));
        }
        return targs;
      } else {
        return "";
      }
    }

    private string InstantiateTemplate(List<Type> typeArgs) {
      if (typeArgs != null) {
        var targs = "";
        if (typeArgs.Count > 0) {
          targs = String.Format("<{0}>", Util.Comma(typeArgs, ta => TypeName(ta, null, null)));
        }

        return targs;
      } else {
        return "";
      }
    }

    protected override string GetHelperModuleName() => "_dafny";

    protected Exception NotSupported(String msg) {
      return new Exception(String.Format("{0} is not yet supported", msg));
    }

    protected Exception NotSupported(String msg, Bpl.IToken tok) {
      return new Exception(String.Format("{0} is not yet supported (at {1}:{2}:{3})", msg, tok.filename, tok.line, tok.col));
    }
    
    protected override IClassWriter CreateClass(string moduleName, string name, bool isExtern, string/*?*/ fullPrintName, List<TypeParameter>/*?*/ typeParameters, TopLevelDecl cls, List<Type>/*?*/ superClasses, Bpl.IToken tok, TargetWriter wr) {
      if (isExtern || (superClasses != null && superClasses.Count > 0)) {
        throw NotSupported(String.Format("extern and/or traits in class {0}", name), tok);
      }

      var classDeclWriter = modDeclWr;
      var classDefWriter = this.classDeclWr;

      if (typeParameters != null && typeParameters.Count > 0) {
        classDeclWriter.WriteLine(DeclareTemplate(typeParameters));
        classDefWriter.WriteLine(DeclareTemplate(typeParameters));
      }

      var methodDeclWriter = classDefWriter.NewBlock(string.Format("class {0}", name), ";");
      var methodDefWriter = wr;

      classDeclWriter.WriteLine("class {0};", name);
      
      methodDeclWriter.Write("public:\n");
      methodDeclWriter.WriteLine("// Default constructor");
      methodDeclWriter.WriteLine("{0}() {{}}", name);

      // Create the code for the specialization of get_default
      var fullName = moduleName + "::" + name;
      var getDefaultStr = String.Format("template <{0}>\nstruct get_default<std::shared_ptr<{1}{2} > > {{\n",
        TypeParameters(typeParameters),
        fullName,
        InstantiateTemplate(typeParameters));
      getDefaultStr += String.Format("static std::shared_ptr<{0}{1} > call() {{\n",
        fullName,
        InstantiateTemplate(typeParameters));
      getDefaultStr += String.Format("return std::shared_ptr<{0}{1} >();", fullName, InstantiateTemplate(typeParameters));
      getDefaultStr += "}\n};";
      this.classDefaults.Add(getDefaultStr);

      var fieldWriter = methodDeclWriter;

      return new ClassWriter(name, this, methodDeclWriter, methodDefWriter, fieldWriter, wr);
    }

    protected override bool SupportsProperties { get => false; }

    protected override IClassWriter CreateTrait(string name, bool isExtern, List<TypeParameter>/*?*/ typeParameters, List<Type>/*?*/ superClasses, Bpl.IToken tok, TargetWriter wr) {
      throw NotSupported(String.Format("traits in class {0}", name), tok);
    }

    protected override BlockTargetWriter CreateIterator(IteratorDecl iter, TargetWriter wr) {
      throw NotSupported(String.Format("iterator {0}", iter));
    }

    protected bool IsRecursiveConstructor(DatatypeDecl dt, DatatypeCtor ctor) {
      foreach (var dtor in ctor.Destructors) {
        if (dtor.Type is UserDefinedType t) {
          if (t.ResolvedClass == dt) {
            return true;
          }
        }
      }
      return false;
    }
    
    protected bool IsRecursiveDatatype(DatatypeDecl dt) {
      foreach (var ctor in dt.Ctors) {
        if (IsRecursiveConstructor(dt, ctor)) {
          return true;
        }
      }
      return false;
    }

    // Uniform naming convention
    protected string DatatypeSubStructName(DatatypeCtor ctor, bool inclTemplateArgs = false) {
      string args = "";
      if (inclTemplateArgs) {
        args = InstantiateTemplate(ctor.EnclosingDatatype.TypeArgs);
      }
      return String.Format("{0}_{1}{2}", IdProtect(ctor.EnclosingDatatype.CompileName), ctor.CompileName, args);
    }

    protected override IClassWriter DeclareDatatype(DatatypeDecl dt, TargetWriter writer) {
      if (dt is TupleTypeDecl) {
        // Tuple types are declared once and for all in DafnyRuntime.h
        return null;
      }

      this.datatypeDecls.Add(dt);

      string DtT = dt.CompileName;
      string DtT_protected = IdProtect(DtT);

      // Forward declaration of the type
      this.modDeclWr.WriteLine("{0}\nstruct {1};", DeclareTemplate(dt.TypeArgs), DtT_protected);
      var wdecl = this.dtDeclWr;
      var wdef = writer;

      if (IsRecursiveDatatype(dt)) { // Note that if this is true, there must be more than one constructor!
        // Add some forward declarations
        wdecl.WriteLine("{0}\nstruct {1};", DeclareTemplate(dt.TypeArgs), DtT_protected);
        wdecl.WriteLine("{2}\nbool operator==(const {0}{1} &left, const {0}{1} &right); ", DtT_protected, InstantiateTemplate(dt.TypeArgs), DeclareTemplate(dt.TypeArgs));
      }

      // Optimize a not-uncommon case
      if (dt.Ctors.Count == 1) {
        var ctor = dt.Ctors[0];
        var ws = wdecl.NewBlock(String.Format("{0}\nstruct {1}", DeclareTemplate(dt.TypeArgs), DtT_protected), ";");

        // Declare the struct members
        var i = 0;
        var argNames = new List<string>();
        foreach (Formal arg in ctor.Formals) {
          if (!arg.IsGhost) {
            ws.WriteLine("{0} {1};", TypeName(arg.Type, wdecl, arg.tok), FormalName(arg, i));
            argNames.Add(FormalName(arg, i));
            i++;
          }
        }

        if (argNames.Count > 0) {
          // Create a constructor with arguments
          ws.Write("{0}(", DtT_protected);
          WriteFormals("", ctor.Formals, ws);
          ws.Write(")");
          if (argNames.Count > 0) {
            // Add initializers
            ws.Write(" :");
            ws.Write(Util.Comma(argNames, nm => String.Format(" {0} ({0})", IdProtect(nm))));
          }

          ws.WriteLine(" {}");
        }

        // Create a constructor with no arguments
        ws.WriteLine("{0}();", DtT_protected);
        var wc = wdef.NewNamedBlock("{1}\n{0}{2}::{0}()", DtT_protected, DeclareTemplate(dt.TypeArgs), InstantiateTemplate(dt.TypeArgs));
        foreach (var arg in ctor.Formals) {
          if (!arg.IsGhost) {
            wc.WriteLine("{0} = {1};", arg.CompileName, DefaultValue(arg.Type, wc, arg.tok));
          }
        }

        // Overload the comparison operator
        var wrCompOp = ws.NewNamedBlock("friend bool operator==(const {0} &left, const {0} &right)", DtT_protected);
        wrCompOp.Write("\treturn true");
        foreach (var arg in argNames) {
          wrCompOp.WriteLine("\t\t&& left.{0} == right.{0}", arg);
        }
        wrCompOp.WriteLine(";");

        // Overload the not-comparison operator
        ws.WriteLine("friend bool operator!=(const {0} &left, const {0} &right) {{ return !(left == right); }} ", DtT_protected);

        wdecl.WriteLine("{0}\ninline bool is_{1}(const struct {2}{3} d) {{ (void) d; return true; }}", DeclareTemplate(dt.TypeArgs), ctor.CompileName, DtT_protected, InstantiateTemplate(dt.TypeArgs));

        // Define a custom hasher
        hashWr.WriteLine("template <{0}>", TypeParameters(dt.TypeArgs));
        var fullName = dt.Module.CompileName + "::" + DtT_protected + InstantiateTemplate(dt.TypeArgs);
        var hwr = hashWr.NewBlock(string.Format("struct std::hash<{0}>", fullName), ";");
        var owr = hwr.NewBlock(string.Format("std::size_t operator()(const {0}& x) const", fullName));
        owr.WriteLine("size_t seed = 0;");
        foreach (var arg in ctor.Formals) {
          if (!arg.IsGhost) {
            owr.WriteLine("hash_combine<{0}>(seed, x.{1});", TypeName(arg.Type, owr, dt.tok), arg.CompileName);
          }
        }
        owr.WriteLine("return seed;");
      } else {

        /*** Create one struct for each constructor ***/
        foreach (var ctor in dt.Ctors) {
          string structName = DatatypeSubStructName(ctor);
          var wstruct = wdecl.NewBlock(String.Format("{0}\nstruct {1}", DeclareTemplate(dt.TypeArgs), structName), ";");
          // Declare the struct members
          var i = 0;
          foreach (Formal arg in ctor.Formals) {
            if (!arg.IsGhost) {
              if (arg.Type is UserDefinedType udt && udt.ResolvedClass == dt) {  // Recursive declaration needs to use a pointer
                wstruct.WriteLine("std::shared_ptr<{0}> {1};", TypeName(arg.Type, wdecl, arg.tok), FormalName(arg, i));
              } else {
                wstruct.WriteLine("{0} {1};", TypeName(arg.Type, wdecl, arg.tok), FormalName(arg, i));
              }
              i++;
            }
          }

          // Overload the comparison operator
          wstruct.WriteLine("friend bool operator==(const {0} &left, const {0} &right) {{ ", structName);

          var preReturn = wstruct.Fork();
          wstruct.Write("\treturn true ");
          i = 0;
          foreach (Formal arg in ctor.Formals) {
            if (!arg.IsGhost) {
              if (arg.Type is UserDefinedType udt && udt.ResolvedClass == dt) {  // Recursive destructor needs to use a pointer
                wstruct.WriteLine("\t\t&& *(left.{0}) == *(right.{0})", FormalName(arg, i));
              } else {
                wstruct.WriteLine("\t\t&& left.{0} == right.{0}", FormalName(arg, i));
              }
              i++;
            }
          }

          if (i == 0) { // Avoid a warning from the C++ compiler
            preReturn.WriteLine("(void)left; (void) right;");
          }

          wstruct.WriteLine(";\n}");

          // Overload the not-comparison operator
          wstruct.WriteLine("friend bool operator!=(const {0} &left, const {0} &right) {{ return !(left == right); }} ", structName);

          // Define a custom hasher
          hashWr.WriteLine("template <{0}>", TypeParameters(dt.TypeArgs));
          var fullName = dt.Module.CompileName + "::" + structName + InstantiateTemplate(dt.TypeArgs);
          var hwr = hashWr.NewBlock(string.Format("struct std::hash<{0}>", fullName), ";");
          var owr = hwr.NewBlock(string.Format("std::size_t operator()(const {0}& x) const", fullName));
          owr.WriteLine("size_t seed = 0;");
          int argCount = 0;
          foreach (var arg in ctor.Formals) {
            if (!arg.IsGhost) {
              if (arg.Type is UserDefinedType udt && udt.ResolvedClass == dt) {
                // Recursive destructor needs to use a pointer
                owr.WriteLine("hash_combine<std::shared_ptr<{0}>>(seed, x.{1});", TypeName(arg.Type, owr, dt.tok), arg.CompileName);
              } else {
                owr.WriteLine("hash_combine<{0}>(seed, x.{1});", TypeName(arg.Type, owr, dt.tok), arg.CompileName);
              }
              argCount++;
            }
          }
          if (argCount == 0) {
            owr.WriteLine("(void)x;");
          }
          owr.WriteLine("return seed;");
        }

        /*** Declare the overall tagged union ***/
        var ws = wdecl.NewBlock(String.Format("{0}\nstruct {1}", DeclareTemplate(dt.TypeArgs), DtT_protected), ";");
        ws.WriteLine("std::variant<{0}> v;", Util.Comma(dt.Ctors, ctor => DatatypeSubStructName(ctor, true)));

        // Declare static "constructors" for each Dafny constructor
        foreach (var ctor in dt.Ctors)
        {
          using (var wc = ws.NewNamedBlock("static {0} create_{1}({2})",
                                           DtT_protected, ctor.CompileName,
                                           DeclareFormals(ctor.Formals))) {
            wc.WriteLine("{0}{1} COMPILER_result;", DtT_protected, InstantiateTemplate(dt.TypeArgs));
            wc.WriteLine("{0} COMPILER_result_subStruct;", DatatypeSubStructName(ctor, true));

            foreach (Formal arg in ctor.Formals)
            {
              if (!arg.IsGhost) {
                if (arg.Type is UserDefinedType udt && udt.ResolvedClass == dt) {
                  // This is a recursive destuctor, so we need to allocate space and copy the input in
                  wc.WriteLine("COMPILER_result_subStruct.{0} = std::make_shared<{1}>({0});", arg.CompileName, DtT_protected);
                } else {
                  wc.WriteLine("COMPILER_result_subStruct.{0} = {0};", arg.CompileName);
                }
              }
            }
            wc.WriteLine("COMPILER_result.v = COMPILER_result_subStruct;");
            wc.WriteLine("return COMPILER_result;");
          }
        }

        // Declare a default constructor
        ws.WriteLine("{0}();", DtT_protected);
        using (var wd = wdef.NewNamedBlock(String.Format("{1}\n{0}{2}::{0}()", DtT_protected, DeclareTemplate(dt.TypeArgs), InstantiateTemplate(dt.TypeArgs)))) {
          var default_ctor = dt.Ctors[0];   // Arbitrarily choose the first one
          wd.WriteLine("{0} COMPILER_result_subStruct;", DatatypeSubStructName(default_ctor, true));
          foreach (Formal arg in default_ctor.Formals)
          {
            if (!arg.IsGhost) {
              wd.WriteLine("COMPILER_result_subStruct.{0} = {1};", arg.CompileName,
                DefaultValue(arg.Type, wd, arg.tok));
            }
          }
          wd.WriteLine("v = COMPILER_result_subStruct;");
        }

        // Declare a default destructor
        ws.WriteLine("~{0}() {{}}", DtT_protected);

        // Declare a default copy constructor (just in case any of our components are non-trivial, i.e., contain smart_ptr)
        using (var wcc = ws.NewNamedBlock(String.Format("{0}(const {0} &other)", DtT_protected))) {
          wcc.WriteLine("v = other.v;");
        }

        // Declare a default copy assignment operator (just in case any of our components are non-trivial, i.e., contain smart_ptr)
        using (var wcc = ws.NewNamedBlock(String.Format("{0}& operator=(const {0} other)", DtT_protected))) {
          wcc.WriteLine("v = other.v;");
          wcc.WriteLine("return *this;");
        }

        // Declare type queries, both as members and general-purpose functions
        foreach (var ctor in dt.Ctors) {
          var name = DatatypeSubStructName(ctor);
          var holds = String.Format("std::holds_alternative<{0}{1}>", name, InstantiateTemplate(dt.TypeArgs));
          ws.WriteLine("bool is_{0}() const {{ return {1}(v); }}", name, holds);
          wdecl.WriteLine("{0}\ninline bool is_{1}(const struct {2}{3} d);", DeclareTemplate(dt.TypeArgs), name, DtT_protected, InstantiateTemplate(dt.TypeArgs));
           wdef.WriteLine("{0}\ninline bool is_{1}(const struct {2}{3} d) {{ return {4}(d.v); }}",
             DeclareTemplate(dt.TypeArgs), name, DtT_protected, InstantiateTemplate(dt.TypeArgs), holds);
        }

        // Overload the comparison operator
        ws.WriteLine("friend bool operator==(const {0} &left, const {0} &right) {{ ", DtT_protected);
        ws.WriteLine("\treturn left.v == right.v;\n}");

        // Create destructors
        foreach (var ctor in dt.Ctors) {
          foreach (var dtor in ctor.Destructors) {
            if (dtor.EnclosingCtors[0] == ctor) {
              var arg = dtor.CorrespondingFormals[0];
              if (!arg.IsGhost && arg.HasName) {
                var returnType = TypeName(arg.Type, ws, arg.tok);
                if (arg.Type is UserDefinedType udt && udt.ResolvedClass == dt) {
                  // This is a recursive destuctor, so return a pointer
                  returnType = String.Format("std::shared_ptr<{0}>", returnType);
                }
                using (var wDtor = ws.NewNamedBlock("{0} dtor_{1}()", returnType,
                  arg.CompileName)) {
                  if (dt.IsRecordType) {
                    wDtor.WriteLine("return this.{0};", IdName(arg));
                  } else {
                    var n = dtor.EnclosingCtors.Count;
                    for (int i = 0; i < n - 1; i++) {
                      var ctor_i = dtor.EnclosingCtors[i];
                      var ctor_name = DatatypeSubStructName(ctor_i);
                      Contract.Assert(arg.CompileName == dtor.CorrespondingFormals[i].CompileName);
                      wDtor.WriteLine("if (is_{0}()) {{ return std::get<{0}{1}>(v).{2}; }}",
                        ctor_name, InstantiateTemplate(dt.TypeArgs), IdName(arg));
                    }

                    Contract.Assert(arg.CompileName == dtor.CorrespondingFormals[n - 1].CompileName);
                    var final_ctor_name = DatatypeSubStructName(dtor.EnclosingCtors[n - 1], true);
                    wDtor.WriteLine("return std::get<{0}>(v).{1}; ",
                      final_ctor_name, IdName(arg));
                  }
                }
              }
            }
          }
        }

        // Overload the not-comparison operator
        ws.WriteLine("friend bool operator!=(const {0} &left, const {0} &right) {{ return !(left == right); }} ", DtT_protected);

        // Define a custom hasher for the struct as a whole
        hashWr.WriteLine("template <{0}>", TypeParameters(dt.TypeArgs));
        var fullStructName = dt.Module.CompileName + "::" + DtT_protected;
        var hwr2 = hashWr.NewBlock(string.Format("struct std::hash<{0}{1}>", fullStructName, InstantiateTemplate(dt.TypeArgs)), ";");
        var owr2 = hwr2.NewBlock(string.Format("std::size_t operator()(const {0}{1}& x) const", fullStructName, InstantiateTemplate(dt.TypeArgs)));
        owr2.WriteLine("size_t seed = 0;");
        var index = 0;
        foreach (var ctor in dt.Ctors) {
          var ifwr = owr2.NewBlock(string.Format("if (x.is_{0}())", DatatypeSubStructName(ctor)));
          ifwr.WriteLine("hash_combine<uint64>(seed, {0});", index);
          ifwr.WriteLine("hash_combine<struct {0}::{1}>(seed, std::get<{0}::{1}>(x.v));", dt.Module.CompileName, DatatypeSubStructName(ctor, true));
          index++;
        }
        owr2.WriteLine("return seed;");

        if (IsRecursiveDatatype(dt)) {
          // Emit a custom hasher for a pointer to this type
          hashWr.WriteLine("template <{0}>", TypeParameters(dt.TypeArgs));
          hwr2 = hashWr.NewBlock(string.Format("struct std::hash<std::shared_ptr<{0}{1}>>", fullStructName, InstantiateTemplate(dt.TypeArgs)), ";");
          owr2 = hwr2.NewBlock(string.Format("std::size_t operator()(const std::shared_ptr<{0}{1}>& x) const", fullStructName, InstantiateTemplate(dt.TypeArgs)));
          owr2.WriteLine("struct std::hash<{0}{1}> hasher;", fullStructName, InstantiateTemplate(dt.TypeArgs));
          owr2.WriteLine("std::size_t h = hasher(*x);");
          owr2.WriteLine("return h;");
        }
      }

      return null;
    }

    protected override IClassWriter DeclareNewtype(NewtypeDecl nt, TargetWriter wr) {
      if (nt.NativeType != null) {
        if (nt.NativeType.Name != nt.Name) {
          string nt_name_def, literalSuffice_def;
          bool needsCastAfterArithmetic_def;
          GetNativeInfo(nt.NativeType.Sel, out nt_name_def, out literalSuffice_def, out needsCastAfterArithmetic_def);
          wr.WriteLine("typedef {0} {1};", nt_name_def, nt.Name);
        }
      } else {
        throw NotSupported(String.Format("non-native newtype {0}", nt));
      }
      var className = "class_" + IdName(nt);
      var cw = CreateClass(nt.Module.CompileName, className, null, wr) as CppCompiler.ClassWriter;
      var w = cw.MethodDeclWriter;
      if (nt.WitnessKind == SubsetTypeDecl.WKind.Compiled) {
        var witness = new TargetWriter(w.IndentLevel, true);
        if (nt.NativeType == null) {
          TrExpr(nt.Witness, witness, false);
        } else {
          TrParenExpr(nt.Witness, witness, false);
          witness.Write(".toNumber()");
        }
        DeclareField(className, nt.TypeArgs, "Witness", true, true, nt.BaseType, nt.tok, witness.ToString(), w, wr);
      }

      string nt_name, literalSuffice;
      bool needsCastAfterArithmetic;
      GetNativeInfo(nt.NativeType.Sel, out nt_name, out literalSuffice, out needsCastAfterArithmetic);
      using (var wDefault = w.NewBlock(string.Format("static {0} get_Default()", nt_name))) {
        var udt = new UserDefinedType(nt.tok, nt.Name, nt, new List<Type>());
        var d = TypeInitializationValue(udt, wr, nt.tok, false);
        wDefault.WriteLine("return {0};", d);
      }

      return cw;
    }

    protected override void DeclareSubsetType(SubsetTypeDecl sst, TargetWriter wr) {
      if (sst.Name == "nat") {
        return;  // C++ does not support Nats
      }

      string templateDecl = "";
      if (sst.Var.Type is SeqType s) {
        templateDecl = DeclareTemplate(s.TypeArgs[0].TypeArgs);  // We want the type args (if any) for the seq-elt type, not the seq
      } else {
        templateDecl = DeclareTemplate(sst.Var.Type.TypeArgs);
      }

      this.modDeclWr.WriteLine("{2} using {1} = {0};", TypeName(sst.Var.Type, wr, sst.tok), IdName(sst), templateDecl);

      var className = "class_" + IdName(sst);
      var cw = CreateClass(sst.Module.CompileName, className, sst, wr) as CppCompiler.ClassWriter;
      var w = cw.MethodDeclWriter;

      if (sst.WitnessKind == SubsetTypeDecl.WKind.Compiled) {
        var witness = new TargetWriter(w.IndentLevel, true);
        TrExpr(sst.Witness, witness, false);
        DeclareField(className, sst.TypeArgs, "Witness", true, true, sst.Rhs, sst.tok, witness.ToString(), w, wr);
      }

      using (var wDefault = w.NewBlock(String.Format("static {0}{1} get_Default()", IdName(sst), InstantiateTemplate(sst.TypeArgs)))) {
        var udt = new UserDefinedType(sst.tok, sst.Name, sst, sst.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp)));
        var d = TypeInitializationValue(udt, wr, sst.tok, false);
        wDefault.WriteLine("return {0};", d);
      }
    }

    protected override void GetNativeInfo(NativeType.Selection sel, out string name, out string literalSuffix, out bool needsCastAfterArithmetic) {
      literalSuffix = "";
      needsCastAfterArithmetic = false;
      switch (sel) {
        case NativeType.Selection.Byte:
          name = "uint8";
          break;
        case NativeType.Selection.SByte:
          name = "int8";
          break;
        case NativeType.Selection.UShort:
          name = "uint16";
          break;
        case NativeType.Selection.Short:
          name = "int16";
          break;
        case NativeType.Selection.UInt:
          name = "uint32";
          break;
        case NativeType.Selection.Int:
          name = "int32";
          break;
        case NativeType.Selection.ULong:
          name = "uint64";
          break;
        case NativeType.Selection.Number:
        case NativeType.Selection.Long:
          name = "int64";
          break;
        default:
          Contract.Assert(false);  // unexpected native type
          throw new cce.UnreachableException();  // to please the compiler
      }
    }

    protected class ClassWriter : IClassWriter {
      public string ClassName;
      public readonly CppCompiler Compiler;
      public readonly BlockTargetWriter MethodDeclWriter;
      public readonly TargetWriter MethodWriter;
      public readonly BlockTargetWriter FieldWriter;
      public readonly TargetWriter Finisher;

      public ClassWriter(string className, CppCompiler compiler, BlockTargetWriter methodDeclWriter, TargetWriter methodWriter, BlockTargetWriter fieldWriter, TargetWriter finisher) {
        Contract.Requires(compiler != null);
        Contract.Requires(methodDeclWriter != null);
        Contract.Requires(methodWriter != null);
        Contract.Requires(fieldWriter != null);
        this.ClassName = className;
        this.Compiler = compiler;
        this.MethodDeclWriter = methodDeclWriter;
        this.MethodWriter = methodWriter;
        this.FieldWriter = fieldWriter;
        this.Finisher = finisher;
      }

      public BlockTargetWriter/*?*/ CreateMethod(Method m, bool createBody) {
        return Compiler.CreateMethod(m, createBody, MethodDeclWriter, MethodWriter);
      }
      public BlockTargetWriter/*?*/ CreateFunction(string name, List<TypeParameter>/*?*/ typeArgs, List<Formal> formals, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, MemberDecl member) {
        return Compiler.CreateFunction(member.EnclosingClass.CompileName, member.EnclosingClass.TypeArgs, name, typeArgs, formals, resultType, tok, isStatic, createBody, MethodDeclWriter, MethodWriter);
      }
      public BlockTargetWriter/*?*/ CreateGetter(string name, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, MemberDecl/*?*/ member) {
        return Compiler.CreateGetter(name, resultType, tok, isStatic, createBody, MethodWriter);
      }
      public BlockTargetWriter/*?*/ CreateGetterSetter(string name, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, MemberDecl/*?*/ member, out TargetWriter setterWriter) {
        return Compiler.CreateGetterSetter(name, resultType, tok, isStatic, createBody, out setterWriter, MethodWriter);
      }
      public void DeclareField(string name, TopLevelDecl enclosingDecl, bool isStatic, bool isConst, Type type, Bpl.IToken tok, string rhs) {
        Compiler.DeclareField(ClassName, enclosingDecl.TypeArgs, name, isStatic, isConst, type, tok, rhs, FieldWriter, Finisher);
      }
      public void InitializeField(Field field, Type instantiatedFieldType, TopLevelDeclWithMembers enclosingClass) {
        throw new NotSupportedException();  // InitializeField should be called only for those compilers that set ClassesRedeclareInheritedFields to false.
      }
      public TextWriter/*?*/ ErrorWriter() => MethodWriter;
      public void Finish() { }
    }

    protected BlockTargetWriter/*?*/ CreateMethod(Method m, bool createBody, BlockTargetWriter wdr, TargetWriter wr) {
      List<Formal> nonGhostOuts = m.Outs.Where(o => !o.IsGhost).ToList();
      string targetReturnTypeReplacement = null;
      if (nonGhostOuts.Count == 1) {
        targetReturnTypeReplacement = TypeName(nonGhostOuts[0].Type, wr, nonGhostOuts[0].tok);
      } else if (nonGhostOuts.Count > 1) {
        targetReturnTypeReplacement = String.Format("struct Tuple{0}{1}", nonGhostOuts.Count, InstantiateTemplate(nonGhostOuts.ConvertAll(n => n.Type)));
      }

      if (!createBody) {
        return null;
      }

      if (m.TypeArgs.Count != 0) {
        wdr.WriteLine(DeclareTemplate(m.TypeArgs));
        wr.WriteLine(DeclareTemplate(m.TypeArgs));
      }

      if (m.EnclosingClass.TypeArgs != null && m.EnclosingClass.TypeArgs.Count > 0) {
        wr.WriteLine(DeclareTemplate(m.EnclosingClass.TypeArgs));
      }

      wr.Write("{0} {1}{2}::{3}",
        targetReturnTypeReplacement ?? "void",
        m.EnclosingClass.CompileName,
        InstantiateTemplate(m.EnclosingClass.TypeArgs),
        IdName(m));

      wdr.Write("{0}{1} {2}",
        m.IsStatic ? "static " : "",
        targetReturnTypeReplacement ?? "void",
        IdName(m));

      wr.Write("(");
      wdr.Write("(");
      int nIns = WriteFormals("", m.Ins, wr);
      WriteFormals("", m.Ins, wdr);
      if (targetReturnTypeReplacement == null) {
        WriteFormals(nIns == 0 ? "" : ", ", m.Outs, wr);
        WriteFormals(nIns == 0 ? "" : ", ", m.Outs, wdr);
      }
      wdr.Write(");\n");

      var w = wr.NewBlock(")", null, BlockTargetWriter.BraceStyle.Newline, BlockTargetWriter.BraceStyle.Newline);

      if (targetReturnTypeReplacement != null) {
        var r = new TargetWriter(w.IndentLevel);
        EmitReturn(m.Outs, r);
        w.BodySuffix = r.ToString();
      }
      return w;
    }

    protected BlockTargetWriter/*?*/ CreateFunction(string className,  List<TypeParameter> classArgs, string name, List<TypeParameter>/*?*/ typeArgs, List<Formal> formals, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, BlockTargetWriter wdr, TargetWriter wr) {
      if (!createBody) {
        return null;
      }

      if (typeArgs.Count != 0) {
        wdr.WriteLine(DeclareTemplate(typeArgs));
        wr.WriteLine(DeclareTemplate(typeArgs));
      }
      if (classArgs != null && classArgs.Count != 0) {
        wr.WriteLine(DeclareTemplate(typeArgs));
      }

      wdr.Write("{0}{1} {2}",
        isStatic ? "static " : "",
        TypeName(resultType, wr, tok),
        name);
      wr.Write("{0} {1}{2}::{3}",
        TypeName(resultType, wr, tok),
        className,
        InstantiateTemplate(classArgs),
        name);

      wdr.Write("(");
      wr.Write("(");
      WriteFormals("", formals, wdr);
      int nIns = WriteFormals("", formals, wr);

      wdr.Write(");");
      var w = wr.NewBlock(")", null, BlockTargetWriter.BraceStyle.Newline, BlockTargetWriter.BraceStyle.Newline);

      return w;
    }

    protected override int EmitRuntimeTypeDescriptorsActuals(List<TypeArgumentInstantiation> typeArgs, Bpl.IToken tok, bool useAllTypeArgs, TargetWriter wr) {
      var sep = "";
      var c = 0;
      foreach (var ta in typeArgs) {
        if (useAllTypeArgs || ta.Formal.Characteristics.MustSupportZeroInitialization) {
          wr.Write("{0}{1}", sep, RuntimeTypeDescriptor(ta.Actual, tok, wr));
          sep = ", ";
          c++;
        }
      }
      return c;
    }

    string RuntimeTypeDescriptor(Type type, Bpl.IToken tok, TextWriter wr) {
      Contract.Requires(type != null);
      Contract.Requires(tok != null);
      Contract.Requires(wr != null);
      throw NotSupported(string.Format("RuntimeTypeDescriptor {0} not yet supported", type), tok);
    }

    protected BlockTargetWriter/*?*/ CreateGetter(string name, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, TargetWriter wr) {
      // We don't use getters
      return createBody ? new TargetWriter().NewBlock("") : null;
    }

    protected BlockTargetWriter/*?*/ CreateGetterSetter(string name, Type resultType, Bpl.IToken tok, bool isStatic, bool createBody, out TargetWriter setterWriter, TargetWriter wr) {
      // We don't use getter/setter pairs; we just embed the trait's fields.
      if (createBody) {
        var abyss = new TargetWriter();
        setterWriter = abyss;
        return abyss.NewBlock("");
      } else {
        setterWriter = null;
        return null;
      }
    }

    protected override BlockTargetWriter EmitTailCallStructure(MemberDecl member, BlockTargetWriter wr) {
      wr.WriteLine("TAIL_CALL_START:");
      return wr;
    }

    protected override void EmitJumpToTailCallStart(TargetWriter wr) {
      wr.WriteLine("goto TAIL_CALL_START;");
    }

    protected void Warn(string msg, Bpl.IToken tok) {
      Console.Error.WriteLine("WARNING: {3} ({0}:{1}:{2})", tok.filename, tok.line, tok.col, msg);
    }

    // Because we use reference counting (via shared_ptr), the TypeName of a class differs
    // depending on whether we are declaring a variable or talking about the class itself.
    // Use class_name = true if you want the actual name of the class, not the type used when declaring variables/arguments/etc.
    protected string TypeName(Type type, TextWriter wr, Bpl.IToken tok, MemberDecl/*?*/ member = null, bool class_name=false) {
      Contract.Ensures(Contract.Result<string>() != null);
      Contract.Assume(type != null);  // precondition; this ought to be declared as a Requires in the superclass

      var xType = type.NormalizeExpand();
      if (xType is TypeProxy) {
        // unresolved proxy; just treat as ref, since no particular type information is apparently needed for this type
        return "object";
      }

      if (xType is BoolType) {
        return "bool";
      } else if (xType is CharType) {
        return "char";
      } else if (xType is IntType || xType is BigOrdinalType) {
        Warn("BigInteger used", tok);
        return "BigNumber";
      } else if (xType is RealType) {
        Warn("BigRational used", tok);
        return "Dafny.BigRational";
      } else if (xType is BitvectorType) {
        var t = (BitvectorType)xType;
        return t.NativeType != null ? GetNativeTypeName(t.NativeType) : "BigNumber";
      } else if (xType.AsNewtype != null) {
        NativeType nativeType = xType.AsNewtype.NativeType;
        if (nativeType != null) {
          return GetNativeTypeName(nativeType);
        }
        return TypeName(xType.AsNewtype.BaseType, wr, tok);
      } else if (xType.IsObjectQ) {
        return "object";
      } else if (xType.IsArrayType) {
        ArrayClassDecl at = xType.AsArrayType;
        Contract.Assert(at != null);  // follows from type.IsArrayType
        Type elType = UserDefinedType.ArrayElementType(xType);
        if (at.Dims == 1) {
          return "DafnyArray<" + TypeName(elType, wr, tok, null, false) + ">";
        } else {
          throw NotSupported("Multi-dimensional arrays");
        }
      } else if (xType is UserDefinedType) {
        var udt = (UserDefinedType)xType;
        var s = FullTypeName(udt, member);
        var cl = udt.ResolvedClass;
        bool isHandle = true;
        if (cl != null && Attributes.ContainsBool(cl.Attributes, "handle", ref isHandle) && isHandle) {
          return "ulong";
        } else if (DafnyOptions.O.IronDafny &&
            !(xType is ArrowType) &&
            cl != null &&
            cl.Module != null &&
            !cl.Module.IsDefaultModule) {
          s = cl.FullCompileName;
        }
        if (class_name || xType.IsTypeParameter || xType.IsDatatype) {  // Don't add pointer decorations to class names or type parameters
          return IdProtect(s) + ActualTypeArgs(xType.TypeArgs);
        } else {
          return TypeName_UDT(s, udt.TypeArgs, wr, udt.tok);
        }
      } else if (xType is SetType) {
        Type argType = ((SetType)xType).Arg;
        if (ComplicatedTypeParameterForCompilation(argType)) {
          Error(tok, "compilation of set<TRAIT> is not supported; consider introducing a ghost", wr);
        }
        return DafnySetClass + "<" + TypeName(argType, wr, tok) + ">";
      } else if (xType is SeqType) {
        Type argType = ((SeqType)xType).Arg;
        if (ComplicatedTypeParameterForCompilation(argType)) {
          Error(tok, "compilation of seq<TRAIT> is not supported; consider introducing a ghost", wr);
        }
        return DafnySeqClass + "<" + TypeName(argType, wr, tok) + ">";
      } else if (xType is MultiSetType) {
        Type argType = ((MultiSetType)xType).Arg;
        if (ComplicatedTypeParameterForCompilation(argType)) {
          Error(tok, "compilation of multiset<TRAIT> is not supported; consider introducing a ghost", wr);
        }
        return DafnyMultiSetClass + "<" + TypeName(argType, wr, tok) + ">";
      } else if (xType is MapType) {
        Type domType = ((MapType)xType).Domain;
        Type ranType = ((MapType)xType).Range;
        if (ComplicatedTypeParameterForCompilation(domType) || ComplicatedTypeParameterForCompilation(ranType)) {
          Error(tok, "compilation of map<TRAIT, _> or map<_, TRAIT> is not supported; consider introducing a ghost", wr);
        }
        return DafnyMapClass + "<" + TypeName(domType, wr, tok) + "," + TypeName(ranType, wr, tok) + ">";
      } else {
        Contract.Assert(false); throw new cce.UnreachableException();  // unexpected type
      }
    }

    protected override string TypeName(Type type, TextWriter wr, Bpl.IToken tok, MemberDecl/*?*/ member = null) {
      Contract.Ensures(Contract.Result<string>() != null);
      Contract.Assume(type != null);  // precondition; this ought to be declared as a Requires in the superclass
      return TypeName(type, wr, tok, member, false);
    }
   
    public override string TypeInitializationValue(Type type, TextWriter/*?*/ wr, Bpl.IToken/*?*/ tok, bool inAutoInitContext) {
      var xType = type.NormalizeExpandKeepConstraints();
      if (xType is BoolType) {
        return "false";
      } else if (xType is CharType) {
        return "'D'";
      } else if (xType is IntType || xType is BigOrdinalType) {
        Warn("BigInteger used.  Code will not compile.", tok);
        return "new BigNumber(0)";
      } else if (xType is RealType) {
        Warn("BigRational used.  Code will not compile.", tok);
        return "_dafny.BigRational.ZERO";
      } else if (xType is BitvectorType) {
        var t = (BitvectorType)xType;
        if (t.NativeType != null) {
          return "0";
        } else {
          Warn("Non-native bitvector type used.  Code will not compile.", tok);
          return "new BigNumber(0)";
        }
      } else if (xType is SetType) {
        var s = (SetType) xType;
        return String.Format("DafnySet<{0}>::empty()", TypeName(s.Arg, wr, tok));
      } else if (xType is MultiSetType) {
        throw NotSupported("MultiSets");
      } else if (xType is SeqType) {
        return string.Format("DafnySequence<{0}>()", TypeName(xType.AsSeqType.Arg, wr, tok, null, false));
      } else if (xType is MapType) {
        var m = (MapType) xType;
        return String.Format("DafnyMap<{0},{1}>::empty()", TypeName(m.Domain, wr, tok), TypeName(m.Range, wr, tok));
      }

      var udt = (UserDefinedType)xType;
      if (udt.ResolvedParam != null) {
        if (udt.ResolvedClass != null && Attributes.Contains(udt.ResolvedClass.Attributes, "extern")) {
          // Assume the external definition includes a default value
          return String.Format("{1}::get_{0}_default()", IdProtect(udt.Name), udt.ResolvedClass.Module.CompileName);
        } else if (inAutoInitContext && !udt.ResolvedParam.Characteristics.MustSupportZeroInitialization) {
          return String.Format("get_default<{0}>::call()", IdProtect(udt.Name));
        } else {
          return String.Format("get_default<{0}>::call()", IdProtect(udt.Name));
        }
      }
      var cl = udt.ResolvedClass;
      Contract.Assert(cl != null);
      if (cl is NewtypeDecl) {
        var td = (NewtypeDecl)cl;
        if (td.Witness != null) {
          return td.Module.CompileName + "::class_" + td.CompileName + "::Witness";
        } else if (td.NativeType != null) {
          return "0";
        } else {
          return TypeInitializationValue(td.BaseType, wr, tok, inAutoInitContext);
        }
      } else if (cl is SubsetTypeDecl) {
        var td = (SubsetTypeDecl)cl;
        if (td.Witness != null) {
          return td.Module.CompileName + "::class_" + td.CompileName + "::Witness";
        } else if (td.WitnessKind == SubsetTypeDecl.WKind.Special) {
          // WKind.Special is only used with -->, ->, and non-null types:
          Contract.Assert(ArrowType.IsPartialArrowTypeName(td.Name) || ArrowType.IsTotalArrowTypeName(td.Name) || td is NonNullTypeDecl);
          if (ArrowType.IsPartialArrowTypeName(td.Name)) {
            return "nullptr";
          } else if (ArrowType.IsTotalArrowTypeName(td.Name)) {
            var rangeDefaultValue = TypeInitializationValue(udt.TypeArgs.Last(), wr, tok, inAutoInitContext);
            // return the lambda expression ((Ty0 x0, Ty1 x1, Ty2 x2) => rangeDefaultValue)
            return string.Format("function () {{ return {0}; }}", rangeDefaultValue);
          } else if (((NonNullTypeDecl)td).Class is ArrayClassDecl) {
            // non-null array type; we know how to initialize them
            var arrayClass = (ArrayClassDecl)((NonNullTypeDecl)td).Class;
            Type elType = UserDefinedType.ArrayElementType(xType);
            if (arrayClass.Dims == 1) {
              return string.Format("DafnyArray<{0}>::Null()", TypeName(elType, wr, tok));
            } else {
              return string.Format("_dafny.newArray(nullptr, {0})", Util.Comma(arrayClass.Dims, _ => "0"));
            }
          } else {
            // non-null (non-array) type
            // even though the type doesn't necessarily have a known initializer, it could be that the the compiler needs to
            // lay down some bits to please the C++ compiler's different definite-assignment rules.
            return "nullptr";
          }
        } else {
          return TypeInitializationValue(td.RhsWithArgument(udt.TypeArgs), wr, tok, inAutoInitContext);
        }
      } else if (cl is ClassDecl) {
        bool isHandle = true;
        if (Attributes.ContainsBool(cl.Attributes, "handle", ref isHandle) && isHandle) {
          return "0";
        } else {
          if (cl is ArrayClassDecl) {
            var arrayClass = (ArrayClassDecl)cl;
            Type elType = UserDefinedType.ArrayElementType(xType);
            if (arrayClass.Dims == 1) {
              return string.Format("DafnyArray<{0}>::Null()", TypeName(elType, wr, tok));
            } else {
              throw NotSupported("Multi-dimensional arrays");
            }
          } else {
            return "nullptr";
          }
        }
      } else if (cl is DatatypeDecl) {
        var dt = (DatatypeDecl)cl;
        var s = dt is TupleTypeDecl ? "Tuple" + (dt as TupleTypeDecl).Dims : FullTypeName(udt);
        var w = new TargetWriter();
        w.Write("{0}{1}()", s, InstantiateTemplate(udt.TypeArgs));
        return w.ToString();
      } else {
        Contract.Assert(false); throw new cce.UnreachableException();  // unexpected type
      }

    }

    private string ActualTypeArgs(List<Type> typeArgs) {
      return typeArgs.Count > 0
        ? String.Format(" <{0}> ", Util.Comma(typeArgs, tp => TypeName(tp, null, null))) : "";
    }

    protected override string TypeName_UDT(string fullCompileName, List<Type> typeArgs, TextWriter wr, Bpl.IToken tok) {
      Contract.Assume(fullCompileName != null);  // precondition; this ought to be declared as a Requires in the superclass
      Contract.Assume(typeArgs != null);  // precondition; this ought to be declared as a Requires in the superclass
      string s = IdProtect(fullCompileName);
      return String.Format("std::shared_ptr<{0}{1}>", s, ActualTypeArgs(typeArgs));
    }

    protected override string TypeName_Companion(Type type, TextWriter wr, Bpl.IToken tok, MemberDecl/*?*/ member) {
      // There are no companion classes for Cpp
      var t = TypeName(type, wr, tok, member, true);
      return t;
    }

    // ----- Declarations -------------------------------------------------------------
    protected override void DeclareExternType(OpaqueTypeDecl d, Expression compileTypeHint, TargetWriter wr) {
      if (compileTypeHint.AsStringLiteral() == "struct") {
        modDeclWr.WriteLine("// Extern declaration of {1}\n{0} struct {1} {2};", DeclareTemplate(d.TypeArgs), d.Name, InstantiateTemplate(d.TypeArgs));
      } else {
        Error(d.tok, "Opaque type ('{0}') with unrecognized extern attribute {1} cannot be compiled.  Expected {{:extern compile_type_hint}}, e.g., 'struct'.", wr, d.FullName, compileTypeHint.AsStringLiteral());
      }
    }

    protected void DeclareField(string className, List<TypeParameter> targs, string name, bool isStatic, bool isConst, Type type, Bpl.IToken tok, string rhs, TargetWriter wr, TargetWriter finisher) {
      var r = rhs != null ? rhs : DefaultValue(type, wr, tok);
      var t = TypeName(type, wr, tok);
      if (isStatic) {
          wr.WriteLine("static {0} {1};", t, name);
          finisher.WriteLine("{5} {0} {1}{4}::{2} = {3};", t, className, name, r, InstantiateTemplate(targs), DeclareTemplate(targs));
      } else {
        wr.WriteLine("{0} {1} = {2};", t, name, r);
      }
    }

    private string DeclareFormalString(string prefix, string name, Type type, Bpl.IToken tok, bool isInParam) {
      if (isInParam) {
        return String.Format("{0}{2} {1}", prefix, name, TypeName(type, null, tok));
      } else {
        return null;
      }
    }

    protected override bool DeclareFormal(string prefix, string name, Type type, Bpl.IToken tok, bool isInParam, TextWriter wr) {
      var formal_str = DeclareFormalString(prefix, name, type, tok, isInParam);
      if (formal_str != null) {
        wr.Write(formal_str);
        return true;
      } else {
        return false;
      }
    }

    private string DeclareFormals(List<Formal> formals) {
      var i = 0;
      var ret = "";
      var sep = "";
      foreach (Formal arg in formals) {
        if (!arg.IsGhost) {
          string name = FormalName(arg, i);
          string decl = DeclareFormalString(sep, name, arg.Type, arg.tok, arg.InParam);
          if (decl != null) {
            ret += decl;
            sep = ", ";
          }
          i++;
        }
      }
      return ret;
    }

    protected override void DeclareLocalVar(string name, Type/*?*/ type, Bpl.IToken/*?*/ tok, bool leaveRoomForRhs, string/*?*/ rhs, TargetWriter wr) {
      if (type != null) {
        wr.Write("{0} ", TypeName(type, wr, tok));
      } else {
        wr.Write("auto ");
      }
      wr.Write("{0}", name);
      if (leaveRoomForRhs) {
        Contract.Assert(rhs == null);  // follows from precondition
      } else if (rhs != null) {
        wr.WriteLine(" = {0};", rhs);
      } else {
        wr.WriteLine(";");
      }
    }

    protected override TargetWriter DeclareLocalVar(string name, Type/*?*/ type, Bpl.IToken/*?*/ tok, TargetWriter wr) {
      if (type != null) {
        wr.Write("{0} ", TypeName(type, wr, tok));
      } else {
        wr.Write("auto ");
      }
      wr.Write("{0} = ", name);
      var w = wr.Fork();
      wr.WriteLine(";");
      return w;
    }

    protected override bool UseReturnStyleOuts(Method m, int nonGhostOutCount) => true;

    protected override void DeclareOutCollector(string collectorVarName, TargetWriter wr) {
      wr.Write("auto {0} = ", collectorVarName);
    }

    protected override void DeclareLocalOutVar(string name, Type type, Bpl.IToken tok, string rhs, bool useReturnStyleOuts, TargetWriter wr) {
      DeclareLocalVar(name, type, tok, false, rhs, wr);
    }

    protected override void EmitOutParameterSplits(string outCollector, List<string> actualOutParamNames, TargetWriter wr) {
      if (actualOutParamNames.Count == 1) {
        EmitAssignment(actualOutParamNames[0], null, outCollector, null, wr);
      } else {
        for (var i = 0; i < actualOutParamNames.Count; i++) {
          wr.WriteLine("{0} = {1}.get_{2}();", actualOutParamNames[i], outCollector, i);
        }
      }
    }

    protected override void EmitActualTypeArgs(List<Type> typeArgs, Bpl.IToken tok, TextWriter wr) {
      wr.Write(ActualTypeArgs(typeArgs));
    }

    protected override string GenerateLhsDecl(string target, Type/*?*/ type, TextWriter wr, Bpl.IToken tok) {
      return "auto " + target;
    }

    protected void EmitNullText(Type type, TextWriter wr) {
      var xType = type.NormalizeExpand();
      if (xType.IsArrayType) {
        ArrayClassDecl at = xType.AsArrayType;
        Contract.Assert(at != null);  // follows from xType.IsArrayType
        Type elType = UserDefinedType.ArrayElementType(xType);
        if (at.Dims == 1) {
          wr.Write("DafnyArray<{0}>::Null()", TypeName(elType, wr, null));
        } else {
          throw NotSupported("Multi-dimensional arrays");
        }
      } else {
        wr.Write("nullptr");
      }
    }

    protected override void EmitNull(Type type, TargetWriter wr) {
      EmitNullText(type, wr);
    }

    // ----- Statements -------------------------------------------------------------

    protected override void EmitPrintStmt(TargetWriter wr, Expression arg) {
      wr.Write("std::cout << (");
      TrExpr(arg, wr, false);
      wr.WriteLine(");");
    }

    protected override void EmitReturn(List<Formal> outParams, TargetWriter wr) {
      outParams = outParams.Where(f => !f.IsGhost).ToList();
      if (!outParams.Any()) {
        wr.WriteLine("return;");
      } else if (outParams.Count == 1) {
        wr.WriteLine("return {0};", IdName(outParams[0]));
      } else {
        wr.WriteLine("return Tuple{0}{1}({2});", outParams.Count, InstantiateTemplate(outParams.ConvertAll(o => o.Type)), Util.Comma(outParams, IdName));
      }
    }

    protected override TargetWriter CreateLabeledCode(string label, TargetWriter wr) {
      var w = wr.ForkSection();
      wr.IndentLess();
      wr.WriteLine("after_{0}: ;", label);
      return w;
    }

    protected override void EmitBreak(string/*?*/ label, TargetWriter wr) {
      if (label == null) {
        wr.WriteLine("break;");
      } else {
        wr.WriteLine("goto after_{0};", label);
      }
    }

    protected override void EmitYield(TargetWriter wr) {
      throw NotSupported("EmitYield");
    }

    protected override void EmitAbsurd(string/*?*/ message, TargetWriter wr) {
      if (message == null) {
        message = "unexpected control point";
      }
      wr.WriteLine("throw \"{0}\";", message);
    }

    protected override void EmitHalt(Expression messageExpr, TargetWriter wr) {
      wr.Write("throw DafnyHaltException(");
      TrExpr(messageExpr, wr, false);
      wr.WriteLine(");");
    }

    protected override BlockTargetWriter CreateForLoop(string indexVar, string bound, TargetWriter wr) {
      return wr.NewNamedBlock("for (auto {0} = 0; {0} < {1}; {0}++)", indexVar, bound);
    }

    protected override BlockTargetWriter CreateDoublingForLoop(string indexVar, int start, TargetWriter wr) {
      return wr.NewNamedBlock("for (unsigned long long {0} = 1; ; {0} = {0} * 2)", indexVar, start);
    }

    protected override void EmitIncrementVar(string varName, TargetWriter wr) {
      wr.WriteLine("{0} += 1;", varName);
    }

    protected override void EmitDecrementVar(string varName, TargetWriter wr) {
      wr.WriteLine("{0} = {0} -= 1;", varName);
    }

    protected override string GetQuantifierName(string bvType) {
      throw NotSupported("QuantifierName");
    }

    protected override BlockTargetWriter CreateForeachLoop(string boundVar, Type/*?*/ boundVarType, out TargetWriter collectionWriter, TargetWriter wr, string/*?*/ altBoundVarName = null, Type/*?*/ altVarType = null, Bpl.IToken/*?*/ tok = null) {
      wr.Write("for ({0} {1} : ", boundVarType, boundVar);
      collectionWriter = wr.Fork();
      if (altBoundVarName == null) {
        return wr.NewBlock(")");
      } else if (altVarType == null) {
        return wr.NewBlockWithPrefix(")", "{0} = {1};", altBoundVarName, boundVar);
      } else {
        return wr.NewBlockWithPrefix(")", "auto {0} = {1};", altBoundVarName, boundVar);
      }
    }

    // ----- Expressions -------------------------------------------------------------

    protected override void EmitNew(Type type, Bpl.IToken tok, CallStmt/*?*/ initCall, TargetWriter wr) {
      var cl = (type.NormalizeExpand() as UserDefinedType)?.ResolvedClass;
      if (cl != null && cl.Name == "object") {
        //wr.Write("_dafny.NewObject()");
        NotSupported("Tried to emit new generic object, which C++ doesn't do", tok);
      } else {
        var ctor = initCall == null ? null : (Constructor)initCall.Method;  // correctness of cast follows from precondition of "EmitNew"
        wr.Write("std::make_shared<{0}> (", TypeName(type, wr, tok, null, true));
        var tas = TypeArgumentInstantiation.ListFromClass(cl, type.TypeArgs);
        EmitRuntimeTypeDescriptorsActuals(tas, tok, false, wr);
        string q, n;
        if (ctor != null && ctor.IsExtern(out q, out n)) {
          // the arguments of any external constructor are placed here
          string sep = "";
          for (int i = 0; i < ctor.Ins.Count; i++) {
            Formal p = ctor.Ins[i];
            if (!p.IsGhost) {
              wr.Write(sep);
              TrExpr(initCall.Args[i], wr, false);
              sep = ", ";
            }
          }
        }
        wr.Write(")");
      }
    }

    protected override void EmitNewArray(Type elmtType, Bpl.IToken tok, List<Expression> dimensions, bool mustInitialize, TargetWriter wr) {
      var initValue = mustInitialize ? DefaultValue(elmtType, wr, tok) : null;
      // TODO: Handle initValue
      if (dimensions.Count == 1) {
        // handle the common case of 1-dimensional arrays separately
        wr.Write("DafnyArray<{0}>::New(", TypeName(elmtType, wr, tok));
        TrExpr(dimensions[0], wr, false);
        wr.Write(")");
      } else {
        throw NotSupported("Multi-dimensional arrays", tok);
      }
    }

    protected override void EmitLiteralExpr(TextWriter wr, LiteralExpr e) {
      if (e is StaticReceiverExpr) {
        wr.Write(TypeName(e.Type, wr, e.tok));
      } else if (e.Value == null) {
        EmitNullText(e.Type, wr);
      } else if (e.Value is bool) {
        wr.Write((bool)e.Value ? "true" : "false");
      } else if (e is CharLiteralExpr) {
        var v = (string)e.Value;
        wr.Write("'{0}'", v);
      } else if (e is StringLiteralExpr) {
        var str = (StringLiteralExpr)e;
        // TODO: the string should be converted to a Dafny seq<char>
        TrStringLiteral(str, wr);
      } else if (AsNativeType(e.Type) is NativeType nt) {
        wr.Write("({0}){1}", GetNativeTypeName(nt), (BigInteger)e.Value);
        if ((BigInteger) e.Value > 9223372036854775807) {
          // Avoid compiler warning: integer literal is too large to be represented in a signed integer type
          wr.Write("U");
        }
      } else if (e.Value is BigInteger i) {
        EmitIntegerLiteral(i, wr);
      } else if (e.Value is Basetypes.BigDec) {
        throw NotSupported("EmitLiteralExpr of Basetypes.BigDec");
      } else {
        Contract.Assert(false); throw new cce.UnreachableException();  // unexpected literal
      }
    }
    void EmitIntegerLiteral(BigInteger i, TextWriter wr) {
      Contract.Requires(wr != null);
      wr.Write(i);
    }

    protected override void EmitStringLiteral(string str, bool isVerbatim, TextWriter wr) {
      var n = str.Length;
      wr.Write("DafnySequenceFromString(");
      if (!isVerbatim) {
        wr.Write("\"{0}\"", str);
      } else {
        wr.Write("\"");
        for (var i = 0; i < n; i++) {
          if (str[i] == '\"' && i+1 < n && str[i+1] == '\"') {
            wr.Write("\\\"");
            i++;
          } else if (str[i] == '\\') {
            wr.Write("\\\\");
          } else if (str[i] == '\n') {
            wr.Write("\\n");
          } else if (str[i] == '\r') {
            wr.Write("\\r");
          } else {
            wr.Write(str[i]);
          }
        }
        wr.Write("\"");
      }
      wr.Write(")");
    }

    protected override TargetWriter EmitBitvectorTruncation(BitvectorType bvType, bool surroundByUnchecked, TargetWriter wr) {
      string nativeName = null, literalSuffix = null;
      bool needsCastAfterArithmetic = false;
      if (bvType.NativeType != null) {
        GetNativeInfo(bvType.NativeType.Sel, out nativeName, out literalSuffix, out needsCastAfterArithmetic);
      }

      if (bvType.NativeType == null) {
        throw NotSupported("EmitBitvectorTruncation with BigInteger value");
      } else if (bvType.NativeType.Bitwidth == bvType.Width) {
        // no truncation needed
        return wr;
      } else {
        wr.Write("((");
        var middle = wr.Fork();
        // print in hex, because that looks nice
        wr.Write(") & 0x{0:X}{1})", (1UL << bvType.Width) - 1, literalSuffix);
        return middle;
      }
    }

    protected override void EmitRotate(Expression e0, Expression e1, bool isRotateLeft, TargetWriter wr, bool inLetExprBody, FCE_Arg_Translator tr) {
      throw NotSupported("EmitRotate");
    }

    protected override void EmitEmptyTupleList(string tupleTypeArgs, TargetWriter wr) {
      throw NotSupported("EmitEmptyTupleList");
    }

    protected override TargetWriter EmitAddTupleToList(string ingredients, string tupleTypeArgs, TargetWriter wr) {
      throw NotSupported("EmitAddTupleToList");
    }

    protected override void EmitTupleSelect(string prefix, int i, TargetWriter wr) {
      throw NotSupported("EmitTupleSelect");
    }

    protected override string IdProtect(string name) {
      return PublicIdProtect(name);
    }
    public static string PublicIdProtect(string name) {
      Contract.Requires(name != null);
      switch (name) {
        // Taken from: https://www.w3schools.in/cplusplus-tutorial/keywords/
        // Keywords
        case "asm":
        case "auto":
        case "bool":
        case "break":
        case "case":
        case "catch":
        case "char":
        case "class":
        case "const":
        case "const_cast":
        case "continue":
        case "default":
        case "delete":
        case "do":
        case "double":
        case "dynamic_cast":
        case "else":
        case "enum":
        case "explicit":
        case "export":
        case "extern":
        case "false":
        case "float":
        case "for":
        case "friend":
        case "goto":
        case "if":
        case "inline":
        case "int":
        case "long":
        case "mutable":
        case "namespace":
        case "new":
        case "operator":
        case "private":
        case "protected":
        case "public":
        case "register":
        case "reinterpret_cast":
        case "return":
        case "short":
        case "signed":
        case "sizeof":
        case "static":
        case "static_cast":
        case "struct":
        case "switch":
        case "template":
        case "this":
        case "throw":
        case "true":
        case "try":
        case "typedef":
        case "typeid":
        case "typename":
        case "union":
        case "unsigned":
        case "using":
        case "virtual":
        case "void":
        case "volatile":
        case "wchar_t":
        case "while":

        // Also reserved
        case "And":
        case "and_eq":
        case "bitand":
        case "bitor":
        case "compl":
        case "not":
        case "not_eq":
        case "or":
        case "or_eq":
        case "xor":
        case "xor_eq":
          return name + "_";
        default:
          return name;
      }
    }

    protected override string FullTypeName(UserDefinedType udt, MemberDecl/*?*/ member = null) {
      Contract.Assume(udt != null);  // precondition; this ought to be declared as a Requires in the superclass
      if (udt is ArrowType) {
        throw NotSupported(string.Format("UserDefinedTypeName {0}", udt.Name));
        //return ArrowType.Arrow_FullCompileName;
      }
      var cl = udt.ResolvedClass;
      if (cl == null) {
        return IdProtect(udt.CompileName);
      } else if (cl is ClassDecl cdecl && cdecl.IsDefaultClass && Attributes.Contains(cl.Module.Attributes, "extern") &&
                 member != null && Attributes.Contains(member.Attributes, "extern")) {
        // omit the default class name ("_default") in extern modules, when the class is used to qualify an extern member
        Contract.Assert(!cl.Module.IsDefaultModule); // default module is not marked ":extern"
        return IdProtect(cl.Module.CompileName);
      } else if (Attributes.Contains(cl.Attributes, "extern")) {
        return IdProtect(cl.Module.CompileName) + "::" + IdProtect(cl.Name);
      } else if (cl is TupleTypeDecl) {
        var tuple = cl as TupleTypeDecl;
        return "Tuple" + tuple.Dims;
      } else {
        return IdProtect(cl.Module.CompileName) + "::" + IdProtect(cl.CompileName);
      }
    }

    protected override void EmitThis(TargetWriter wr) {
      wr.Write("this");
    }

    protected override void EmitDatatypeValue(DatatypeValue dtv, string arguments, TargetWriter wr) {
      EmitDatatypeValue(dtv, dtv.Ctor, dtv.IsCoCall, arguments, wr);
    }

    void EmitDatatypeValue(DatatypeValue dtv, DatatypeCtor ctor, bool isCoCall, string arguments, TargetWriter wr) {
      var dt = dtv.Ctor.EnclosingDatatype;
      var dtName = dt.CompileName;
      var ctorName = ctor.CompileName;

      if (dt is TupleTypeDecl) {
        var tuple = dt as TupleTypeDecl;
        var types = new List<Type>();
        foreach (var arg in dtv.Arguments) {
          types.Add(arg.Type);
        }
        wr.Write("Tuple{0}{1}({2})", tuple.Dims, InstantiateTemplate(types), arguments);
      } else if (!isCoCall) {
        // Ordinary constructor (that is, one that does not guard any co-recursive calls)
        // Generate:  Dt.create_Ctor(arguments)
        if (dt.Ctors.Count == 1) {
          wr.Write("{3}::{0}{1}({2})",
            dtName,
            InstantiateTemplate(dt.TypeArgs),
            arguments,
            dt.Module.CompileName);
        } else {
          wr.Write("{4}::{0}{1}::create_{2}({3})",
            dtName, ActualTypeArgs(dtv.InferredTypeArgs), ctorName,
            arguments, dt.Module.CompileName);
        }

      } else {
        // Co-recursive call
        // Generate:  Dt.lazy_Ctor(($dt) => Dt.create_Ctor($dt, args))
        wr.Write("{0}.lazy_{1}(($dt) => ", dtName, ctorName);
        wr.Write("{0}.create_{1}($dt{2}{3})", dtName, ctorName, arguments.Length == 0 ? "" : ", ", arguments);
        wr.Write(")");
      }
    }

    protected override void GetSpecialFieldInfo(SpecialField.ID id, object idParam, out string compiledName, out string preString, out string postString) {
      compiledName = "";
      preString = "";
      postString = "";
      switch (id) {
        case SpecialField.ID.UseIdParam:
          compiledName = (string)idParam;
          break;
        case SpecialField.ID.ArrayLength:
        case SpecialField.ID.ArrayLengthInt:
          throw NotSupported("taking an array's length");
        case SpecialField.ID.Floor:
          compiledName = "int()";
          break;
        case SpecialField.ID.IsLimit:
          throw NotSupported("IsLimit");
        case SpecialField.ID.IsSucc:
          throw NotSupported("IsSucc");
        case SpecialField.ID.Offset:
          throw NotSupported("Offset");
        case SpecialField.ID.IsNat:
          throw NotSupported("IsNat");
        case SpecialField.ID.Keys:
          compiledName = "dafnyKeySet()";
          break;
        case SpecialField.ID.Values:
          compiledName = "dafnyValues()";
          break;
        case SpecialField.ID.Items:
          throw NotSupported("Items");
        case SpecialField.ID.Reads:
          compiledName = "_reads";
          break;
        case SpecialField.ID.Modifies:
          compiledName = "_modifies";
          break;
        case SpecialField.ID.New:
          compiledName = "_new";
          break;
        default:
          Contract.Assert(false); // unexpected ID
          break;
      }
    }

    protected override ILvalue EmitMemberSelect(Action<TargetWriter> obj, Type objType, MemberDecl member, List<TypeArgumentInstantiation> typeArgs, Dictionary<TypeParameter, Type> typeMap,
      Type expectedType, string/*?*/ additionalCustomParameter = null, bool internalAccess = false) {
      if (member.IsStatic && member is ConstantField) {
        return SuffixLvalue(obj, "::{0}", member.CompileName);
      } else if (member is DatatypeDestructor dtor && dtor.EnclosingClass is TupleTypeDecl) {
        return SuffixLvalue(obj, ".get_{0}()", dtor.Name);
      } else if (member is SpecialField sf2 && sf2.SpecialId == SpecialField.ID.UseIdParam && sf2.IdParam is string fieldName
                 && fieldName.StartsWith("is_")) {
        // Ugly hack of a check to figure out if this is a datatype query: f.Constructor?
        return SuffixLvalue(obj, ".is_{0}_{1}()", IdProtect(sf2.EnclosingClass.CompileName), fieldName.Substring(3));
      } else if (member is SpecialField sf) {
        string compiledName, preStr, postStr;
        GetSpecialFieldInfo(sf.SpecialId, sf.IdParam, out compiledName, out preStr, out postStr);
        if (sf.SpecialId == SpecialField.ID.Keys || sf.SpecialId == SpecialField.ID.Values) {
          return SuffixLvalue(obj, ".{0}", compiledName);
        } else if (sf is DatatypeDestructor dtor2) {
          if (dtor2.EnclosingCtors.Count > 1) {
            NotSupported(
              String.Format("Using the same destructor {0} with multiple constructors is ambiguous", member.Name),
              dtor2.tok);
          }

          if (!(dtor2.EnclosingClass is IndDatatypeDecl)) {
            NotSupported(
              String.Format("Unexpected use of a destructor {0} that isn't for an inductive datatype.  Panic!",
                member.Name), dtor2.tok);
          }

          var dt = dtor2.EnclosingClass as IndDatatypeDecl;
          return SimpleLvalue(wr => {
            if (dt.Ctors.Count > 1) {
              if (dtor2.Type is UserDefinedType udt && udt.ResolvedClass == dt) {
                // This a recursively defined datatype; need to dereference the pointer
                wr.Write("*");
              }

              wr.Write("(");
              obj(wr);
              wr.Write(".dtor_{0}()", sf.CompileName);
            } else {
              wr.Write("(");
              obj(wr);
              wr.Write(".{0}", sf.CompileName);
            }

            wr.Write(")");
          });
        } else if (!member.IsStatic && compiledName.Length != 0) {
          return SuffixLvalue(obj, "->{0}", compiledName);
        } else if (compiledName.Length != 0) {
          return SuffixLvalue(obj, "::{0}", compiledName);
        } else {
          // this member selection is handled by some kind of enclosing function call, so nothing to do here
          return SimpleLvalue(obj);
        }
      } else if (member is Function) {
        return StringLvalue(String.Format("{0}::{1}::{2}",
          IdProtect(member.EnclosingClass.Module.CompileName),
          IdName(member.EnclosingClass),
          IdName(member)
        ));
      } else {
        return SuffixLvalue(obj, "->{0}", IdName(member));
      }
    }

    protected override TargetWriter EmitArraySelect(List<string> indices, Type elmtType, TargetWriter wr) {
      var w = wr.Fork();
      foreach (var index in indices) {
        wr.Write(".at({0})", index);
      }
      return w;
    }

    protected override TargetWriter EmitArraySelect(List<Expression> indices, Type elmtType, bool inLetExprBody, TargetWriter wr) {
      Contract.Assert(indices != null && 1 <= indices.Count);  // follows from precondition
      var w = wr.Fork();
      foreach (var index in indices) {
        wr.Write(".at(");
        TrExpr(index, wr, inLetExprBody);
        wr.Write(")");
      }
      return w;
    }

    protected override string ArrayIndexToInt(string arrayIndex) {
      return arrayIndex;
    }

    protected override void EmitExprAsInt(Expression expr, bool inLetExprBody, TargetWriter wr) {
      TrParenExpr(expr, wr, inLetExprBody);
      if (AsNativeType(expr.Type) == null) {
        wr.Write(".toNumber()");
      }
    }

    protected override void EmitIndexCollectionSelect(Expression source, Expression index, bool inLetExprBody, TargetWriter wr) {
      TrParenExpr(source, wr, inLetExprBody);
      if (source.Type.NormalizeExpand() is SeqType) {
        // seq
        wr.Write(".select(");
        TrExpr(index, wr, inLetExprBody);
        wr.Write(")");
      } else {
        // map or imap
        wr.Write(".get(");
        TrExpr(index, wr, inLetExprBody);
        wr.Write(")");
      }
    }

    protected override void EmitIndexCollectionUpdate(Expression source, Expression index, Expression value, bool inLetExprBody, TargetWriter wr, bool nativeIndex = false) {
      TrParenExpr(source, wr, inLetExprBody);
      wr.Write(".update(");
      TrExpr(index, wr, inLetExprBody);
      wr.Write(", ");
      TrExpr(value, wr, inLetExprBody);
      wr.Write(")");
    }

    protected override void EmitSeqSelectRange(Expression source, Expression/*?*/ lo, Expression/*?*/ hi, bool fromArray, bool inLetExprBody, TargetWriter wr) {
      if (fromArray) {
        string typeName = "";

        if (source.Type.TypeArgs.Count == 0 && source.Type is UserDefinedType udt && udt.ResolvedClass != null &&
            udt.ResolvedClass is TypeSynonymDecl tsd) {
          // Hack to workaround type synonyms wrapped around the actual array type
          // TODO: Come up with a more systematic way of resolving this!
          typeName = TypeName(tsd.Rhs.TypeArgs[0], wr, source.tok, null, false);
        } else {
          typeName = TypeName(source.Type.TypeArgs[0], wr, source.tok, null, false);
        }
        if (lo == null) {
          if (hi == null) {
            wr.Write("DafnySequence<{0}>::SeqFromArray", typeName);
            TrParenExpr(source, wr, inLetExprBody);
          } else {
            wr.Write("DafnySequence<{0}>::SeqFromArrayPrefix(", typeName);
            TrParenExpr(source, wr, inLetExprBody);
            wr.Write(",");
            TrParenExpr(hi, wr, inLetExprBody);
            wr.Write(")");
          }
        } else {
          if (hi == null) {
            wr.Write("DafnySequence<{0}>::SeqFromArraySuffix(", typeName);
            TrParenExpr(source, wr, inLetExprBody);
            wr.Write(",");
            TrParenExpr(lo, wr, inLetExprBody);
            wr.Write(")");
          } else {
            wr.Write("DafnySequence<{0}>::SeqFromArraySlice(", typeName);
            TrParenExpr(source, wr, inLetExprBody);
            wr.Write(",");
            TrParenExpr(lo, wr, inLetExprBody);
            wr.Write(",");
            TrParenExpr(hi, wr, inLetExprBody);
            wr.Write(")");
          }
        }
      } else {
        TrParenExpr(source, wr, inLetExprBody);

        if (hi != null) {
          TrParenExpr(".take", hi, wr, inLetExprBody);
        }
        if (lo != null) {
          TrParenExpr(".drop", lo, wr, inLetExprBody);
        }
      }
    }

    protected override void EmitSeqConstructionExpr(SeqConstructionExpr expr, bool inLetExprBody, TargetWriter wr) {
      wr.Write("DafnySequence<{0}>::Create(", TypeName(expr.Type, wr, expr.tok, null, false));
      TrExpr(expr.N, wr, inLetExprBody);
      wr.Write(", ");
      TrExpr(expr.Initializer, wr, inLetExprBody);
      wr.Write(")");
    }

    protected override void EmitMultiSetFormingExpr(MultiSetFormingExpr expr, bool inLetExprBody, TargetWriter wr) {
      throw NotSupported("MultiSetFormingExpr", expr.tok);
    }

    protected override void EmitApplyExpr(Type functionType, Bpl.IToken tok, Expression function, List<Expression> arguments, bool inLetExprBody, TargetWriter wr) {
      TrParenExpr(function, wr, inLetExprBody);
      TrExprList(arguments, wr, inLetExprBody);
    }

    protected override TargetWriter EmitBetaRedex(List<string> boundVars, List<Expression> arguments, string typeArgs, List<Type> boundTypes, Type resultType, Bpl.IToken tok, bool inLetExprBody, TargetWriter wr) {
      wr.Write("(({0}) => ", Util.Comma(boundVars));
      var w = wr.Fork();
      wr.Write(")");
      TrExprList(arguments, wr, inLetExprBody);
      return w;
    }

    protected override void EmitConstructorCheck(string source, DatatypeCtor ctor, TargetWriter wr) {
      wr.Write("is_{1}({0})", source, DatatypeSubStructName(ctor));
    }

    protected override void EmitDestructor(string source, Formal dtor, int formalNonGhostIndex, DatatypeCtor ctor, List<Type> typeArgs, Type bvType, TargetWriter wr) {
      if (ctor.EnclosingDatatype is TupleTypeDecl) {
        wr.Write("({0}).get_{1}()", source, formalNonGhostIndex);
      } else {
        var dtorName = FormalName(dtor, formalNonGhostIndex);
        if (dtor.Type is UserDefinedType udt && udt.ResolvedClass == ctor.EnclosingDatatype) {
          // Recursively defined datatype requires a dereference here
          wr.Write("*");
        }

        if (ctor.EnclosingDatatype.Ctors.Count > 1) {
          wr.Write("(({0}).dtor_{1}())", source, dtorName);
        } else {
          wr.Write("(({0}).{1})", source, dtorName);
        }
      }
    }

    protected override BlockTargetWriter CreateLambda(List<Type> inTypes, Bpl.IToken tok, List<string> inNames, Type resultType, TargetWriter wr, bool untyped = false) {
      wr.Write("function (");
      Contract.Assert(inTypes.Count == inNames.Count);  // guaranteed by precondition
      for (var i = 0; i < inNames.Count; i++) {
        wr.Write("{0}{1}", i == 0 ? "" : ", ", inNames[i]);
      }
      var w = wr.NewExprBlock(")");
      return w;
    }

    protected override TargetWriter CreateIIFE_ExprBody(Expression source, bool inLetExprBody, Type sourceType, Bpl.IToken sourceTok, Type resultType, Bpl.IToken resultTok, string bvName, TargetWriter wr) {
      var w = wr.NewExprBlock("[&]({0} {1}) -> {2} ", TypeName(sourceType, wr, sourceTok), bvName, TypeName(resultType, wr, resultTok));
      w.Write("return ");
      w.BodySuffix = ";" + w.NewLine;
      TrParenExpr(source, wr, inLetExprBody);
      return w;
    }

    protected override TargetWriter CreateIIFE_ExprBody(string source, Type sourceType, Bpl.IToken sourceTok, Type resultType, Bpl.IToken resultTok, string bvName, TargetWriter wr) {
      var w = wr.NewExprBlock("[&]({0} {1}) -> {2} ", TypeName(sourceType, wr, sourceTok), bvName, TypeName(resultType, wr, resultTok));
      w.Write("return ");
      w.BodySuffix = ";" + w.NewLine;
      wr.Write("({0})", source);
      return w;
    }

    protected override BlockTargetWriter CreateIIFE0(Type resultType, Bpl.IToken resultTok, TargetWriter wr) {
      var w = wr.NewBigExprBlock("[&] ", " ()");
      return w;
    }

    protected override BlockTargetWriter CreateIIFE1(int source, Type resultType, Bpl.IToken resultTok, string bvName, TargetWriter wr) {
      throw NotSupported("CreateIIFE1", resultTok);
    }

    protected override void EmitUnaryExpr(ResolvedUnaryOp op, Expression expr, bool inLetExprBody, TargetWriter wr) {
      switch (op) {
        case ResolvedUnaryOp.BoolNot:
          TrParenExpr("!", expr, wr, inLetExprBody);
          break;
        case ResolvedUnaryOp.BitwiseNot:
          if (AsNativeType(expr.Type) != null) {
            wr.Write("~ ");
            TrParenExpr(expr, wr, inLetExprBody);
          } else {
            TrParenExpr(expr, wr, inLetExprBody);
            wr.Write(".Not()");
          }
          break;
        case ResolvedUnaryOp.Cardinality:
          TrParenExpr(expr, wr, inLetExprBody);
          wr.Write(".size()");
          break;
        default:
          Contract.Assert(false); throw new cce.UnreachableException();  // unexpected unary expression
      }
    }

    bool IsDirectlyComparable(Type t) {
      Contract.Requires(t != null);
      return t.IsBoolType || t.IsCharType || AsNativeType(t) != null;
    }

    protected override void CompileBinOp(BinaryExpr.ResolvedOpcode op,
      Expression e0, Expression e1, Bpl.IToken tok, Type resultType,
      out string opString,
      out string preOpString,
      out string postOpString,
      out string callString,
      out string staticCallString,
      out bool reverseArguments,
      out bool truncateResult,
      out bool convertE1_to_int,
      TextWriter errorWr) {

      opString = null;
      preOpString = "";
      postOpString = "";
      callString = null;
      staticCallString = null;
      reverseArguments = false;
      truncateResult = false;
      convertE1_to_int = false;

      switch (op) {
        case BinaryExpr.ResolvedOpcode.Iff:
          opString = "=="; break;
        case BinaryExpr.ResolvedOpcode.Imp:
          preOpString = "!"; opString = "||"; break;
        case BinaryExpr.ResolvedOpcode.Or:
          opString = "||"; break;
        case BinaryExpr.ResolvedOpcode.And:
          opString = "&&"; break;
        case BinaryExpr.ResolvedOpcode.BitwiseAnd:
          if (AsNativeType(resultType) != null) {
            opString = "&";
          } else {
            callString = "And";
          }
          break;
        case BinaryExpr.ResolvedOpcode.BitwiseOr:
          if (AsNativeType(resultType) != null) {
            opString = "|";
          } else {
            callString = "Or";
          }
          break;
        case BinaryExpr.ResolvedOpcode.BitwiseXor:
          if (AsNativeType(resultType) != null) {
            opString = "^";
          } else {
            callString = "Xor";
          }
          break;

        case BinaryExpr.ResolvedOpcode.EqCommon: {
            if (IsHandleComparison(tok, e0, e1, errorWr)) {
              opString = "==";
            } else if (IsDirectlyComparable(e0.Type)) {
              opString = "==";
            } else if (e0.Type.IsRefType) {
              opString = "==";
            } else {
              //staticCallString = "==";
              opString = "==";
            }
            break;
          }
        case BinaryExpr.ResolvedOpcode.NeqCommon: {
            if (IsHandleComparison(tok, e0, e1, errorWr)) {
              opString = "!=";
              postOpString = "/* handle */";
            } else if (IsDirectlyComparable(e0.Type)) {
              opString = "!=";
            } else if (e0.Type.IsRefType) {
              opString = "!=";
            } else {
              opString = "!=";
            }
            break;
          }

        case BinaryExpr.ResolvedOpcode.Lt:
        case BinaryExpr.ResolvedOpcode.LtChar:
          opString = "<";
          break;
        case BinaryExpr.ResolvedOpcode.Le:
        case BinaryExpr.ResolvedOpcode.LeChar:
          opString = "<=";
          break;
        case BinaryExpr.ResolvedOpcode.Ge:
        case BinaryExpr.ResolvedOpcode.GeChar:
          opString = ">=";
          break;
        case BinaryExpr.ResolvedOpcode.Gt:
        case BinaryExpr.ResolvedOpcode.GtChar:
          opString = ">";
          break;
        case BinaryExpr.ResolvedOpcode.LeftShift:
          if (resultType.IsBitVectorType) {
            truncateResult = true;
          }
          if (AsNativeType(resultType) != null) {
            opString = "<<";
          } else {
            throw NotSupported("LeftShift of non-native type", tok);
          }
          break;
        case BinaryExpr.ResolvedOpcode.RightShift:
          if (AsNativeType(resultType) != null) {
            opString = ">>";
            if (AsNativeType(e1.Type) == null) {
              postOpString = ".Uint64()";
            }
          } else {
            throw NotSupported("LeftShift of non-native type", tok);
          }
          break;
        case BinaryExpr.ResolvedOpcode.Add:
          if (resultType.IsBitVectorType) {
            truncateResult = true;
          }
          if (resultType.IsCharType || AsNativeType(resultType) != null) {
            opString = "+";
          } else {
            throw NotSupported("Add of non-native type", tok);
          }
          break;
        case BinaryExpr.ResolvedOpcode.Sub:
          if (resultType.IsBitVectorType) {
            truncateResult = true;
          }
          if (resultType.IsCharType || AsNativeType(resultType) != null) {
            opString = "-";
          } else {
            throw NotSupported("Subtraction of non-native type", tok);
          }
          break;
        case BinaryExpr.ResolvedOpcode.Mul:
          if (resultType.IsBitVectorType) {
            truncateResult = true;
          }
          if (AsNativeType(resultType) != null) {
            opString = "*";
          } else {
            throw NotSupported("Multiplication of non-native type", tok);
          }
          break;
        case BinaryExpr.ResolvedOpcode.Div:
          if (AsNativeType(resultType) != null) {
            var nt = AsNativeType(resultType);
            if (nt.LowerBound < BigInteger.Zero) {
              // Want Euclidean division for signed types
              staticCallString =  "EuclideanDivision_" + GetNativeTypeName(AsNativeType(resultType));
            } else {
              // Native division is fine for unsigned
              opString = "/";
            }
          } else {
            callString = "DivBy";
          }
          break;
        case BinaryExpr.ResolvedOpcode.Mod:
          if (AsNativeType(resultType) != null) {
            var nt = AsNativeType(resultType);
            if (nt.LowerBound < BigInteger.Zero) {
              // Want Euclidean division for signed types
              staticCallString = "_dafny.Mod" + Capitalize(GetNativeTypeName(AsNativeType(resultType)));
            } else {
              // Native division is fine for unsigned
              opString = "%";
            }
          } else {
            callString = "Modulo";
          }
          break;
        case BinaryExpr.ResolvedOpcode.SetEq:
        case BinaryExpr.ResolvedOpcode.MultiSetEq:
        case BinaryExpr.ResolvedOpcode.MapEq:
        case BinaryExpr.ResolvedOpcode.SeqEq:
          callString = "equals"; break;
        case BinaryExpr.ResolvedOpcode.SetNeq:
        case BinaryExpr.ResolvedOpcode.MultiSetNeq:
        case BinaryExpr.ResolvedOpcode.MapNeq:
        case BinaryExpr.ResolvedOpcode.SeqNeq:
          preOpString = "!"; callString = "equals"; break;
        case BinaryExpr.ResolvedOpcode.ProperSubset:
        case BinaryExpr.ResolvedOpcode.ProperMultiSubset:
          callString = "IsProperSubsetOf"; break;
        case BinaryExpr.ResolvedOpcode.Subset:
        case BinaryExpr.ResolvedOpcode.MultiSubset:
          callString = "IsSubsetOf"; break;
        case BinaryExpr.ResolvedOpcode.Superset:
        case BinaryExpr.ResolvedOpcode.MultiSuperset:
          callString = "IsSupersetOf"; break;
        case BinaryExpr.ResolvedOpcode.ProperSuperset:
        case BinaryExpr.ResolvedOpcode.ProperMultiSuperset:
          callString = "IsProperSupersetOf"; break;
        case BinaryExpr.ResolvedOpcode.Disjoint:
        case BinaryExpr.ResolvedOpcode.MultiSetDisjoint:
          callString = "IsDisjointFrom"; break;
        case BinaryExpr.ResolvedOpcode.InSet:
        case BinaryExpr.ResolvedOpcode.InMultiSet:
        case BinaryExpr.ResolvedOpcode.InMap:
          callString = "contains"; reverseArguments = true; break;
        case BinaryExpr.ResolvedOpcode.NotInSet:
        case BinaryExpr.ResolvedOpcode.NotInMultiSet:
        case BinaryExpr.ResolvedOpcode.NotInMap:
          preOpString = "!"; callString = "contains"; reverseArguments = true; break;
        case BinaryExpr.ResolvedOpcode.Union:
        case BinaryExpr.ResolvedOpcode.MultiSetUnion:
          callString = "Union"; break;
        case BinaryExpr.ResolvedOpcode.Intersection:
        case BinaryExpr.ResolvedOpcode.MultiSetIntersection:
          callString = "Intersection"; break;
        case BinaryExpr.ResolvedOpcode.SetDifference:
        case BinaryExpr.ResolvedOpcode.MultiSetDifference:
          callString = "Difference"; break;

        case BinaryExpr.ResolvedOpcode.ProperPrefix:
          callString = "IsProperPrefixOf"; break;
        case BinaryExpr.ResolvedOpcode.Prefix:
          callString = "IsPrefixOf"; break;
        case BinaryExpr.ResolvedOpcode.Concat:
          callString = "concatenate"; break;
        case BinaryExpr.ResolvedOpcode.InSeq:
          callString = "contains"; reverseArguments = true; break;
        case BinaryExpr.ResolvedOpcode.NotInSeq:
          preOpString = "!"; callString = "contains"; reverseArguments = true; break;

        default:
          Contract.Assert(false); throw new cce.UnreachableException();  // unexpected binary expression
      }
    }

    protected override void EmitIsZero(string varName, TargetWriter wr) {
      wr.Write("{0} == 0", varName);
    }

    protected override void EmitConversionExpr(ConversionExpr e, bool inLetExprBody, TargetWriter wr) {
      if (e.E.Type.IsNumericBased(Type.NumericPersuation.Int) || e.E.Type.IsBitVectorType || e.E.Type.IsCharType) {
        if (e.ToType.IsNumericBased(Type.NumericPersuation.Real)) {
          throw NotSupported("Real numbers", e.tok);
        } else if (e.ToType.IsCharType) {
          wr.Write("_dafny.Char(");
          TrParenExpr(e.E, wr, inLetExprBody);
          wr.Write(".Int32())");
        } else {
          // (int or bv or char) -> (int or bv or ORDINAL)
          var fromNative = AsNativeType(e.E.Type);
          var toNative = AsNativeType(e.ToType);
          if (fromNative != null && toNative != null) {
            // from a native, to a native -- simple!
            wr.Write(GetNativeTypeName(toNative));
            TrParenExpr(e.E, wr, inLetExprBody);
          } else if (e.E.Type.IsCharType) {
            Contract.Assert(fromNative == null);
            if (toNative == null) {
              // char -> big-integer (int or bv or ORDINAL)
              wr.Write("_dafny.IntOfInt32(rune(");
              TrExpr(e.E, wr, inLetExprBody);
              wr.Write("))");
            } else {
              // char -> native
              wr.Write(GetNativeTypeName(toNative));
              TrParenExpr(e.E, wr, inLetExprBody);
            }
          } else if (fromNative == null && toNative == null) {
            // big-integer (int or bv) -> big-integer (int or bv or ORDINAL), so identity will do
            TrExpr(e.E, wr, inLetExprBody);
          } else if (fromNative != null) {
            Contract.Assert(toNative == null); // follows from other checks

            // native (int or bv) -> big-integer (int or bv)
            wr.Write("_dafny.IntOf{0}(", Capitalize(GetNativeTypeName(fromNative)));
            TrExpr(e.E, wr, inLetExprBody);
            wr.Write(')');
          } else {
            // any (int or bv) -> native (int or bv)
            // Consider some optimizations
            var literal = PartiallyEvaluate(e.E);
            UnaryOpExpr u = e.E.Resolved as UnaryOpExpr;
            MemberSelectExpr m = e.E.Resolved as MemberSelectExpr;
            if (literal != null) {
              // Optimize constant to avoid intermediate BigInteger
              wr.Write("{0}({1})", GetNativeTypeName(toNative), literal);
            } else if (u != null && u.Op == UnaryOpExpr.Opcode.Cardinality) {
              wr.Write("({0})(", GetNativeTypeName(toNative));
              TrParenExpr(u.E, wr, inLetExprBody);
              wr.Write(".size())");
            } else if (m != null && m.MemberName == "Length" && m.Obj.Type.IsArrayType) {
              // Optimize .Length to avoid intermediate BigInteger
              wr.Write("({0})(", GetNativeTypeName(toNative));
              TrParenExpr(m.Obj, wr, inLetExprBody);
              wr.Write(".size())");
            } else {
              // no optimization applies; use the standard translation
              TrParenExpr(e.E, wr, inLetExprBody);
              wr.Write(".{0}()", Capitalize(GetNativeTypeName(toNative)));
            }

          }
        }
      } else if (e.E.Type.IsNumericBased(Type.NumericPersuation.Real)) {
        Contract.Assert(AsNativeType(e.E.Type) == null);
        if (e.ToType.IsNumericBased(Type.NumericPersuation.Real)) {
          // real -> real
          Contract.Assert(AsNativeType(e.ToType) == null);
          TrExpr(e.E, wr, inLetExprBody);
        } else {
          // real -> (int or bv)
          TrParenExpr(e.E, wr, inLetExprBody);
          wr.Write(".Int()");
          if (AsNativeType(e.ToType) is NativeType nt) {
            wr.Write(".{0}()", Capitalize(GetNativeTypeName(nt)));
          }
        }
      } else {
        Contract.Assert(e.E.Type.IsBigOrdinalType);
        Contract.Assert(e.ToType.IsNumericBased(Type.NumericPersuation.Int));
        // identity will do
        TrExpr(e.E, wr, inLetExprBody);
      }
    }

    protected override void EmitCollectionDisplay(CollectionType ct, Bpl.IToken tok, List<Expression> elements, bool inLetExprBody, TargetWriter wr) {
      if (ct is SetType) {
        wr.Write("DafnySet<{0}>::Create({{", TypeName(ct.TypeArgs[0], wr, tok, null, false));
        for (var i = 0; i < elements.Count; i++) {
          TrExpr(elements[i], wr, inLetExprBody);
          if (i < elements.Count - 1)  {
            wr.Write(",");
          }
        }
        wr.Write("})");
      } else if (ct is MultiSetType) {
        throw NotSupported("EmitCollectionDisplay/multiset", tok);
      } else {
        Contract.Assert(ct is SeqType);  // follows from precondition
        if (ct.Arg.IsCharType) {
          throw NotSupported("EmitCollectionDisplay/string", tok);
        } else {
          wr.Write("DafnySequence<{0}>::Create({{", TypeName(ct.TypeArgs[0], wr, tok, null, false));
          for (var i = 0; i < elements.Count; i++) {
            TrExpr(elements[i], wr, inLetExprBody);
            if (i < elements.Count - 1)  {
              wr.Write(",");
            }
          }
          wr.Write("})");
        }
      }
    }

    protected override void EmitMapDisplay(MapType mt, Bpl.IToken tok, List<ExpressionPair> elements, bool inLetExprBody, TargetWriter wr) {
      wr.Write("DafnyMap<{0},{1}>::Create({{",
               TypeName(mt.TypeArgs[0], wr, tok, null, false),
               TypeName(mt.TypeArgs[1], wr, tok, null, false));
      string sep = "";
      foreach (ExpressionPair p in elements) {
        wr.Write(sep);
        wr.Write("{");
        TrExpr(p.A, wr, inLetExprBody);
        wr.Write(",");
        TrExpr(p.B, wr, inLetExprBody);
        wr.Write("}");
        sep = ", ";
      }
      wr.Write("})");
    }

    protected override void EmitCollectionBuilder_New(CollectionType ct, Bpl.IToken tok, TargetWriter wr) {
      if (ct is SetType) {
        wr.Write("DafnySet<{0}>()", TypeName(ct.TypeArgs[0], wr, tok, null, false));
      } else {
        throw NotSupported("EmitCollectionBuilder_New/non_set", tok);
      }
    }

    protected override void EmitCollectionBuilder_Add(CollectionType ct, string collName, Expression elmt, bool inLetExprBody, TargetWriter wr) {
      Contract.Assume(ct is SetType || ct is MultiSetType);  // follows from precondition
      if (ct is MultiSetType) {
        throw NotSupported("EmitCollectionBuilder_Add/MultiSetType");
      }
      wr.Write("{0}.set.emplace(", collName);
      TrExpr(elmt, wr, inLetExprBody);
      wr.WriteLine(");");
    }

    protected override TargetWriter EmitMapBuilder_Add(MapType mt, Bpl.IToken tok, string collName, Expression term, bool inLetExprBody, TargetWriter wr) {
      throw NotSupported("EmitMapBuilder_Add", tok);
    }

    protected override string GetCollectionBuilder_Build(CollectionType ct, Bpl.IToken tok, string collName, TargetWriter wr) {
      // collections are built in place
      return collName;
    }

    protected override void EmitSingleValueGenerator(Expression e, bool inLetExprBody, string type, TargetWriter wr) {
      throw NotSupported("EmitSingleValueGenerator", e.tok);
    }

    // ----- Target compilation and execution -------------------------------------------------------------

    public override bool CompileTargetProgram(string dafnyProgramName, string targetProgramText, string/*?*/ callToMain, string/*?*/ targetFilename, ReadOnlyCollection<string> otherFileNames,
      bool hasMain, bool runAfterCompile, TextWriter outputWriter, out object compilationResult) {
      compilationResult = null;
      throw NotSupported("Compilation of C++ files is not yet supported");
    }

    public override bool RunTargetProgram(string dafnyProgramName, string targetProgramText, string/*?*/ callToMain, string targetFilename, ReadOnlyCollection<string> otherFileNames,
      object compilationResult, TextWriter outputWriter) {
        throw NotSupported("Running C++ programs is not yet supported");
    }
  }
}
