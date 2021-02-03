using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Diagnostics.Contracts;
using IToken = Microsoft.Boogie.IToken;

namespace Microsoft.Dafny
{
  public class TokenHelper {
    public static string loc(IToken tok, IToken relative) {
      string result = $"{tok.line}:{tok.col}";
      if (relative!=null && relative.filename!=tok.filename) {
        result = tok.filename + ":" + result;
      }
      return result;
    }
  }

/*
  you can't refine a module functor (because implicit parameters aren't available)
  you can't import a module functor (because implicit parameters aren't available)
  an actual functor parameter to a non-functor module param must refine that 
  for non-functor formals,
    c refines C
  for functor formals,
    c must satisfy exsts x :: not-functor(x) && c refines C(x) && x satisfies this constraint on the formal defn in C
*/

  public class ParameterizedModule {
    public readonly ModuleDefinition def;
    public ParameterizedModule(ModuleDefinition def) {
      this.def = def;
    }

    public AppliedModule apply(List<AppliedModule> actuals)
    {
      ModuleApplicationCloner cloner = new ModuleApplicationCloner(actuals);
      string name = def.Name + "_applied"; // need to get pretty ambitious about unambiguously naming these things

      return new AppliedModule(cloner.CloneModuleDefinition(def, name));
    }
  }

  public class AppliedModule {
    public readonly ModuleDefinition def;

    public AppliedModule(ModuleDefinition def)
    {
      this.def = def;
    }
    public AppliedModule LookupModule(string name)
    {
      foreach (TopLevelDecl decl in def.TopLevelDecls)
      {
        if (decl is AppliedModuleDecl amd) {
          if (amd.Name == name)
          {
            return amd.appliedModule;
          }
        } else if (decl is LiteralModuleDecl lmd) {
          if (lmd.Name == name) {
            return new AppliedModule(lmd.ModuleDef);
          }
        }
      }
      return null;
    }
  }

  public class AppliedModuleDecl : ModuleDecl
  {
    public readonly AppliedModule appliedModule;

    public AppliedModuleDecl(AppliedModule appliedModule, IToken name, ModuleDefinition parent)
      : base(name, name.val, parent, false, false) {
      this.appliedModule = appliedModule;
    }
    
    public override object Dereference() { return appliedModule.def; }
  }

  class ModuleApplicationCloner : Cloner
  {
    List<AppliedModule> actuals;
    public ModuleApplicationCloner(List<AppliedModule> actuals) {
      this.actuals = actuals;
    }

    public override ModuleDefinition CloneModuleDefinition(ModuleDefinition modDef, string name) {
      Contract.Assert(actuals.Count == modDef.Formals.Count);
      if (modDef.Formals.Count==0) {
        return modDef;
      }

      // Construct a new module with AliasModuleDecls replacing the Formals.
      ModuleDefinition newDef = base.CloneModuleDefinition(modDef, name);
      for (int i=0; i<modDef.Formals.Count; i++) {
        AppliedModuleDecl actualAlias = new AppliedModuleDecl(actuals[i], modDef.Formals[i].Name, modDef);
        newDef.TopLevelDecls.Insert(i, actualAlias);
      }
      newDef.Formals = new List<FormalModuleDecl>(); // No formals in the resulting applied module
      return newDef;
    }
  }

  public class SymbolTable<T> {
    readonly Dictionary<String, T> Symbols;
    internal SymbolTable<T> parent;
    internal readonly ErrorReporter reporter;
    internal object debugOrigin;

    public SymbolTable(SymbolTable<T> parent, ErrorReporter reporter, object debugOrigin) {
      this.Symbols = new Dictionary<String, T>();
      this.reporter = reporter;
      this.parent = parent;
      this.debugOrigin = debugOrigin;
    }

    // Returns false if name already appears in symbol table.
    public virtual bool insert(String name, T decl, IToken errTok, Func<T, string> errMsg) {
      if (Symbols.ContainsKey(name))
      {
        reporter.Error(MessageSource.Resolver, errTok, errMsg(Symbols[name]));
        return false;
      }
      Symbols.Add(name, decl);
      return true;
    }

    public virtual T lookup(String name) {
      T result;
      if (Symbols.TryGetValue(name, out result)) {
        return result;
      }
      if (parent != null) {
        return parent.lookup(name);
      }
      return default(T);
    }
  }

  public class RefinementModuleSymbolsView : SymbolTable<AppliedModule>
  {
    public AppliedModule refinementBase;
    readonly Dictionary<String, AppliedModule> baseSyms;
    public SymbolTable<AppliedModule> localSyms;

    public RefinementModuleSymbolsView(AppliedModule refinementBase, SymbolTable<AppliedModule> localSyms, object debugOrigin)
      : base(null, localSyms.reporter, debugOrigin)
    {
      this.refinementBase = refinementBase;
      this.baseSyms = new Dictionary<String, AppliedModule>();
      this.localSyms = localSyms;
      Contract.Assert(localSyms.parent == null);  // because I'm going to peek into its dict
      Console.Out.WriteLine($"Building RefinementModuleSymbolsView for {refinementBase.def.Name}");
      foreach (TopLevelDecl d in refinementBase.def.TopLevelDecls) {
        if (d is ModuleDecl md) {
          if (d is AppliedModuleDecl ad)
          {
            baseSyms.Add(ad.Name, ad.appliedModule); // what goeth here? An AppliedModule
            Console.Out.WriteLine($"Added AppliedModuleDecl {md.Name}");
          } else if (d is LiteralModuleDecl ld) {
            baseSyms.Add(md.Name, new AppliedModule(ld.ModuleDef)); // what goeth here? An AppliedModule
            Console.Out.WriteLine($"Added LiteralModuleDecl {md.Name}");
          } else {
            Console.Out.WriteLine($"XXX What do to with {md.Name}?");
          }
        }
      }
    }
    
//    public override bool insert(String name, AppliedModule decl, IToken errTok, Func<AppliedModule, string> errMsg) {
//    }
    public override AppliedModule lookup(String name)
    {
      AppliedModule baseSym = null;
      bool hasBase = baseSyms.TryGetValue(name, out baseSym);
      AppliedModule localSym = localSyms.lookup(name);
      if (baseSym==null && localSym==null) {
        return null;
      }
      if (baseSym!=null && localSym!=null) {
        reporter.Error(MessageSource.Resolver, localSym.def.tok, $"Symbol {name} shadowed in {refinementBase.def.Name} by definition in {debugOrigin}");
        return null;
      }
      if (baseSym!=null) {
        return baseSym;
      }
      return localSym;
    }
  }

  public class ModuleResolver
  {
    ErrorReporter reporter;

    public ModuleResolver(ErrorReporter reporter)
    {
      this.reporter = reporter;
    }
    
    public ParameterizedModule resolve(LiteralModuleDecl decl)
    {
      return resolveLiteralDecl(decl, new SymbolTable<ParameterizedModule>(null, reporter, "top"));
    }

    public ParameterizedModule resolveLiteralDecl(LiteralModuleDecl decl,
      SymbolTable<ParameterizedModule> outerSyms)
    {
      // ParameterizedModule names are "global" in that the source code talks about other modules
      // using their pre-applied names.
      SymbolTable<ParameterizedModule> globalSyms = new SymbolTable<ParameterizedModule>(outerSyms, reporter, decl);
      // AppliedModules are "local" in that we create them at the site where module application
      // occurs: they contain the module parameters and alias modules, both of which are applied.
      SymbolTable<AppliedModule> localSyms = new SymbolTable<AppliedModule>(null, reporter, decl);

      ModuleDefinition def = decl.ModuleDef;
      // Resolve references in formals
      if (def.Formals != null) {
        foreach (FormalModuleDecl formal in def.Formals)
        {
          var errorCount = reporter.Count(ErrorLevel.Error);
          AppliedModule formalApplied = null;
          if (formal.ConstraintModExp.application.moduleParams.Count == 0) {
            // Case I: the formal module doesn't take parameters, nor did the actual supply any.
            // Case II: the formal module expects nonzero parameters, but the actual didn't supply any.
            //     This is the fancy case, where we use 'module requires' to avoid unrolling all the
            //     formal parameters.
            // This formal isn't applied. If it has its own formals, we imagine there exists values
            // that satisfy them, and then later when this module is applied with a value that
            // whose application satisfy the constraints, there's yer witness.
            formalApplied = resolveModulePathWithoutApplication(formal.ConstraintModExp, globalSyms, localSyms);
          } else {
            // Case III: the formal module expects nonzero parameters, and the actual supplies them.
            // This formal is applied, so we apply it so inside the module we know what additional
            // facts we can enjoy.
            formalApplied = resolveModuleExpr(formal.ConstraintModExp, globalSyms, localSyms);
          }
          if (formalApplied != null) {
            localSyms.insert(formal.Name.val, formalApplied, formal.Name, existing =>
              $"Name {formal.Name.val} already defined at {TokenHelper.loc(existing.def.tok, formal.Name)}.");
          } else {
            Contract.Assert(reporter.Count(ErrorLevel.Error) > errorCount); // an error already been emitted
          }
        }
      }
      
      // Resolve reference in refines
      if (def.RefinementBaseModExp != null)
      {
        AppliedModule refinementBaseApplied
          = resolveModuleExpr(def.RefinementBaseModExp, globalSyms, localSyms);
        // Adjust symbol table to make refinement module symbols available.
        localSyms = new RefinementModuleSymbolsView(refinementBaseApplied, localSyms, "refinement");
      }

      // Literal modules define ParameterizedModules.
      foreach (ModuleDecl subDecl in decl.ModuleDef.TopLevelDecls.Where(d => d is LiteralModuleDecl))
      {
        ParameterizedModule parameterizedModule = resolveLiteralDecl((LiteralModuleDecl) subDecl, globalSyms);
        string name = subDecl.Name;
        globalSyms.insert(name, parameterizedModule, subDecl.tok, existing =>
          $"Name {name} already defined at {TokenHelper.loc(existing.def.tok, subDecl.tok)}.");
      }

      // AliasModuleDecls make AppliedModules out of ParameterizedModules
      foreach (ModuleDecl subDecl in decl.ModuleDef.TopLevelDecls.Where(d => d is AliasModuleDecl))
      {
        AliasModuleDecl alias = (AliasModuleDecl) subDecl;
        AppliedModule aliasApplied
          = resolveModuleExpr(alias.TargetModExp, globalSyms, localSyms);
        if (aliasApplied != null)
        {
          localSyms.insert(alias.Name, aliasApplied, subDecl.tok, existing => {
            return $"Name {alias.Name} already defined at {TokenHelper.loc(existing.def.tok, subDecl.tok)}.";});
        }
      }

      return new ParameterizedModule(decl.ModuleDef);
    }

    public AppliedModule resolveModuleExpr(ModuleExpression modexp,
      SymbolTable<ParameterizedModule> globalSyms, SymbolTable<AppliedModule> localSyms)
    {
      String name = modexp.application.tok.val;
      AppliedModule am = localSyms.lookup(name);
      if (am != null)
      {
        if (!modexp.application.IsSimple())
        {
          reporter.Error(MessageSource.Resolver, modexp.application.tok,
            $"Parameters applied to already-applied module {name}");
          return null;
        }
      }
      else
      {
        ParameterizedModule pm = globalSyms.lookup(name);
        if (pm == null)
        {
          reporter.Error(MessageSource.Resolver, modexp.application.tok, $"Module {name} unbound");
          return null;
        }

        List<AppliedModule> actuals = new List<AppliedModule>();
        foreach (ModuleExpression actualParam in modexp.application.moduleParams)
        {
          Console.Out.WriteLine("XXX-TODO check {actualParam} refines its formal");
          actuals.Add(resolveModuleExpr(actualParam, globalSyms, localSyms));
        }

        if (pm.def.Formals.Count != actuals.Count)
        {
          reporter.Error(MessageSource.Resolver, modexp.application.tok,
              $"Module {pm.def.Name} expects {pm.def.Formals.Count} parameters, got {actuals.Count}");
          return null;
        }
        am = pm.apply(actuals);
      }
      // Whether name was already applied or paramaterized and applied above, we now have an am on which
      // we can resolve the remainder of the path.
      am = resolveModExpPathRemainder(am, modexp);
      return am;
    }

    public AppliedModule resolveModulePathWithoutApplication(ModuleExpression modexp,
      SymbolTable<ParameterizedModule> globalSyms, SymbolTable<AppliedModule> localSyms)
    {
      IToken firstTok = modexp.FirstToken();
      ParameterizedModule pm = globalSyms.lookup(firstTok.val);
      if (pm == null) {
        reporter.Error(MessageSource.Resolver, firstTok, $"Module path element {firstTok.val} not found. (B)");
        return null;
      }
      // XXX Do I need to do anything to prevent formals from being referenced by this process?
      // TODO write test case
      AppliedModule fauxApplied = new AppliedModule(pm.def);
      return resolveModExpPathRemainder(fauxApplied, modexp);
    }

    public AppliedModule resolveModExpPathRemainder(AppliedModule am, ModuleExpression modexp)
    {
      foreach (IToken tok in modexp.path) {
          am = am.LookupModule(tok.val);
          if (am == null)
          {
            reporter.Error(MessageSource.Resolver, tok, $"Module path element {tok.val} not found. (C)");
            return null;
          }
      }
      return am;
    }
  }
}
