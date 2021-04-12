using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

  public interface ModuleView {
    public ModuleView lookup(string name, Dictionary<string, ModuleView> context = null);

    // Construct the of modules to resolve, in dependency order (postorder tree walk).
    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews);

    // Return a decl for this module with Signature populated.
    public ModuleDecl GetDecl();

    public ModuleDefinition GetDef();

    enum RequireApplication { Yes, No }

    static bool equalsOrRefines(ModuleDefinition m0, string m1) {
      if (m0.Name == m1) {
        return true;
      }

      if (m0.RefinementBaseModExp != null && m0.RefinementBaseModExp.application != null && m0.RefinementBaseModExp.application.tok.val == m1) {
        return true;
      }

      if (m0.RefinementBaseModExp != null && m0.RefinementBaseModExp.Def != null) { // m0 has a refinement
        return equalsOrRefines(m0.RefinementBaseModExp.Def, m1);
      }

      return false;
    }

    static ModuleView resolveModuleExpression(ModuleView view, ModuleExpression modExp, ErrorReporter reporter, RequireApplication requireApplication) {
      String name = modExp.application.tok.val;
      ModuleView mv = view.lookup(name);
      if (mv == null) {
        var msg = $"Module Could not find module name {name}";
        reporter.Error(MessageSource.Resolver, modExp.application.tok, msg);
        return new ErrorModuleView();
      }

      ModuleView appliedView;

      // Apply actuals if offered
      if (modExp.application.moduleParams.Count > 0) {
        Contract.Assert(mv is DefModuleView);
        DefModuleView dmv = (DefModuleView) mv;

        List<ModuleView> actuals = new List<ModuleView>();
        foreach (ModuleExpression actualParam in modExp.application.moduleParams)
        {
          //Console.Out.WriteLine("XXX-TODO check {actualParam} refines its formal");
          actuals.Add(ModuleView.resolveModuleExpression(view, actualParam, reporter, requireApplication));
        }

        if (dmv.Def.Formals.Count != actuals.Count) {
          var msg = $"Module {dmv.Def.Name} expects {dmv.Def.Formals.Count} parameters, got {actuals.Count}";
          reporter.Error(MessageSource.Resolver, modExp.application.tok, msg);
          return new ErrorModuleView();
        }

        // Check that each actual parameter has the appropriate module "type"
        foreach (var item in dmv.Def.Formals.Zip(actuals)) {
          var formal = item.First;
          var actual = item.Second;
          if (!equalsOrRefines(actual.GetDef(), formal.ConstraintModExp.application.tok.val)) {
            var msg = $"Module {dmv.Def.Name} expects {formal.ConstraintModExp.application.tok.val}, got {actual.GetDef().Name}";
            reporter.Error(MessageSource.Resolver, modExp.application.tok, msg);
            return new ErrorModuleView();
          }
        }

        appliedView = new ApplicationModuleView(dmv, actuals);
      } else {
        if (mv is DefModuleView dmv) {
          // Is this the only way we can have unfilled formals? What if the thing we're referencing
          // is a previous actual that itself left its formals empty?
          if (requireApplication==RequireApplication.Yes && dmv.Def.Formals.Count > 0) {
            var msg = $"Module {dmv.Def.Name} requires {dmv.Def.Formals.Count} parameters.";
            reporter.Error(MessageSource.Resolver, modExp.application.tok, msg);
            return new ErrorModuleView();
          }
        }
        appliedView = mv;
      }

      // Whether name was already applied or paramaterized and applied above, we now have an am on which
      // we can resolve the remainder of the path.
      ModuleView pathView = ModuleView.resolveModExpPathRemainder(appliedView, modExp, reporter);
      return pathView;
    }

    static ModuleView resolveModExpPathRemainder(ModuleView curView, ModuleExpression modexp, ErrorReporter reporter) {
      foreach (IToken tok in modexp.path) {
          curView = curView.lookup(tok.val);
          if (curView == null)
          {
            reporter.Error(MessageSource.Resolver, tok, $"Module path element {tok.val} not found. (C)");
            return null;
          }
      }
      return curView;
    }
  }

  public class ErrorModuleView : ModuleView
  {
    public ModuleView lookup(string name, Dictionary<string, ModuleView> context = null) {
      return null;
    }

    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews) {
      // Nothing to do
    }

    public ModuleDecl GetDecl() {
      throw new NotImplementedException();
    }

    public ModuleDefinition GetDef() {
      throw new NotImplementedException();
    }
  }

  public class SymbolTableView : ModuleView {
    internal readonly ModuleView Parent;
    internal ModuleView RefinementView;
    readonly Dictionary<string, ModuleView> Underway;
    internal readonly string DebugContext;

    public SymbolTableView(ModuleView parent, string debugContext) {
      this.Parent = parent;
      this.DebugContext = debugContext;
      this.Underway = new Dictionary<string, ModuleView>();
    }

    public override string ToString() {
      return $"Syms({DebugContext})";
    }

    public ModuleView lookup(string name, Dictionary<string, ModuleView> context = null) {
      ModuleView mv;
      if (Underway.TryGetValue(name, out mv)) {
        return mv;
      }
      if (RefinementView != null) {
        mv = RefinementView.lookup(name, context);
        if (mv != null) {
          return mv;
        }
      }
      if (Parent != null) {
        return Parent.lookup(name, context);
      }
      return null;
    }

    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews) {
      Contract.Assert(false); // This class shouldn't survive into the type resolver.
    }

    public ModuleDecl GetDecl() {
      Contract.Assert(false); // This class shouldn't survive into the type resolver.
      return null;
    }

    public ModuleDefinition GetDef()
    {
      Contract.Assert(false);
      return null;
    }

    public void Add(string name, ModuleView mv) {
      Underway.Add(name, mv);
    }

    public void SetRefinementView(ModuleView rv) {
      Contract.Assert(RefinementView == null);
      RefinementView = rv;
    }
  }

  // View of the modules visible inside a module: formals, literals, and stuff from refinement.
  public class DefModuleView : ModuleView {
    public readonly ModuleDefinition Def;
    public readonly LiteralModuleDecl Decl; // keep a ref to the decl, which Resolver.cs will populate with a signature and then want to find that signature later.
    public Dictionary<string, Tuple<ModuleDecl,ModuleView>> LocalViews; // XXX LocalViews is redundant with context.
    public Dictionary<string, ModuleDecl> LocalDecls;
    public readonly ModuleView RefinementView;

    public static DefModuleView FromTopDecl(ModuleDecl defaultModule, ErrorReporter reporter)
    {
      LiteralModuleDecl lmd = (LiteralModuleDecl) defaultModule;
      return new DefModuleView(null, lmd.ModuleDef, lmd, reporter);
    }

    internal DefModuleView(ModuleView parentContext, ModuleDefinition Def, LiteralModuleDecl Decl, ErrorReporter reporter)
    {
      this.Def = Def;
      this.Decl = Decl;
      this.LocalViews = new Dictionary<string, Tuple<ModuleDecl, ModuleView>>();
      SymbolTableView context = new SymbolTableView(parentContext, Def.Name);

      /*
       module A { type T }
       module B(a:A) {}
       module C(b:B) { predicate foo(x:b.a.T) { false } }
       module C(a:A, a2:A, b:B(a), b2:B(a2)) {}
       module C(b:B, b2:B) requires b.A==b2.A {}
       module C(b:B(A)) {}
       module D refines B(A) { //adds behavior }
       module { import F = C(D) }
       */

      // Add the formals first; they're reachable from the refinement expression.
      foreach (FormalModuleDecl formal in Def.Formals) {
        ModuleView fv = ModuleView.resolveModuleExpression(context, formal.ConstraintModExp, reporter, ModuleView.RequireApplication.No);
        // There's no TopLevelDecl here, so we'll roll up an AliasModuleDecl to make
        // the type resolver happy.
        ModuleDecl fd = new AliasModuleDecl(formal.ConstraintModExp, formal.Name, Def, false, null);
        Debug.Assert(fv != null);
        LocalViews.Add(formal.Name.val, new Tuple<ModuleDecl,ModuleView>(fd,fv));
        context.Add(formal.Name.val, fv);
      }

      // Evaluate the refinement expression next.
      RefinementView = null;
      if (Def.RefinementBaseModExp != null) {
        RefinementView = ModuleView.resolveModuleExpression(context, Def.RefinementBaseModExp, reporter, ModuleView.RequireApplication.Yes);
        context.SetRefinementView(RefinementView);  // XXX and now RefinementView is, too.
      }

      // Then resolve the literals and evaluate the aliases inside the body.
      foreach (TopLevelDecl decl in Def.TopLevelDecls)
      {
        if (decl is LiteralModuleDecl lmd) {
          ModuleView lv = new DefModuleView(context, lmd.ModuleDef, lmd, reporter);
          Debug.Assert(lv != null);
          LocalViews.Add(lmd.Name, new Tuple<ModuleDecl,ModuleView>(lmd,lv));
          context.Add(lmd.Name, lv);
        } else if (decl is AliasModuleDecl amd) {
          ModuleView lv = ModuleView.resolveModuleExpression(context, amd.TargetModExp, reporter, ModuleView.RequireApplication.Yes);
          LocalViews.Add(amd.Name, new Tuple<ModuleDecl, ModuleView>(amd, lv));
          context.Add(amd.Name, lv);
        }
      }

      //Console.Out.WriteLine($"new {this.ToString()}");
    }

    public override string ToString() {
      return Def.Name;
    }

    public ModuleView lookup(string name, Dictionary<string, ModuleView> context = null) {
      Tuple<ModuleDecl,ModuleView> lvt;
      if (LocalViews.TryGetValue(name, out lvt)) {
        ModuleView view = lvt.Item2;
        if (view is ApplicationModuleView amv) {
          List<ModuleView> actuals = new List<ModuleView>();
          foreach (var formal in amv.GetDef().Formals) {
            ModuleView actual = null;
            if (context.TryGetValue(formal.Name.val, out actual)) {
              actuals.Add(actual);
            } else {
              actuals.Add(amv.Substitutions.GetValueOrDefault(formal.Name.val));
            }
          }
          view = new ApplicationModuleView(amv.Prototype, actuals);
        }
        return view;
      }
      if (RefinementView != null) {
        return RefinementView.lookup(name, context);
      }
      return null;
    }

/*

    module A { import U }
    module B refines A {}
    module C(b:B) {
      import V = b.U
      predicate foo(x:V.T) {}
    }
    */

    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews) {
      foreach (var (key,lvt) in LocalViews.Select(x => (x.Key, x.Value))) {
        lvt.Item2.BuildModuleVisitList(outModuleViews);
        outModuleViews.Add(lvt);
      }
    }

    public ModuleDecl GetDecl() {
      Contract.Assert(Decl.Signature != null);  // Shouldn't be using this module before it gets type-resolved.
      return Decl;
    }

    public ModuleDefinition GetDef()
    {
      return Def;
    }
  }

  public class ApplicationModuleView : ModuleView {
    public DefModuleView Prototype;
    public Dictionary<string, ModuleView> Substitutions;

    public ApplicationModuleView(DefModuleView prototype, List<ModuleView> actuals) {
      this.Prototype = prototype;
      this.Substitutions = new Dictionary<string, ModuleView>();
      Contract.Assert(prototype.Def.Formals.Count == actuals.Count);
      for (int i=0; i<prototype.Def.Formals.Count; i++) {
        Substitutions.Add(prototype.Def.Formals[i].Name.val, actuals[i]);
      }
      //Console.Out.WriteLine($"new {this.ToString()}");
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder(100);
      sb.Append(Prototype).Append("(");
      for (int i=0; i<Prototype.Def.Formals.Count; i++) {
        if (i>0) { sb.Append(","); }
        string name = Prototype.Def.Formals[i].Name.val;
        //sb.Append(name);
        //sb.Append("=");
        sb.Append(Substitutions[name]);
      }
      sb.Append(")");
      return sb.ToString();
    }

    // Merges two dictionaries, preferring the values from dictA
    // Based on https://stackoverflow.com/a/25213088/11132282
    public static Dictionary<TKey, TValue> Merge<TKey, TValue>(Dictionary<TKey, TValue> dictA, Dictionary<TKey, TValue> dictB)
      where TValue : class
    {
      if (dictA == null) {
        return dictB;
      }

      if (dictB == null) {
        return dictA;
      }

      return dictA.Keys.Union(dictB.Keys).ToDictionary(k => k, k => dictA.ContainsKey(k) ? dictA[k] : dictB[k]);
    }

    public ModuleView lookup(string name, Dictionary<string, ModuleView> context = null) {
      ModuleView mv;
      if (Substitutions.TryGetValue(name, out mv)) {
        return mv;
      }

      Dictionary<string, ModuleView> newContext = Merge(Substitutions, context);
      return Prototype.lookup(name, newContext);
    }

    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews) {
      // All modules referenced by this application must already have been visited
    }

    public ModuleDecl GetDecl() {
      Contract.Assert(false); // unimpl! Do Travis' tricksy substitution.
      return null;
    }

    public ModuleDefinition GetDef()
    {
      return Prototype.GetDef();
    }
  }
}
