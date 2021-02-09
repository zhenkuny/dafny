using System;
using System.Collections.Generic;
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
    public ModuleView lookup(string name);

    // Construct the of modules to resolve, in dependency order (postorder tree walk).
    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews);

    // Return a decl for this module with Signature populated.
    public ModuleDecl GetDecl();

    static ModuleView resolveModuleExpression(ModuleView view, ModuleExpression modExp, ErrorReporter reporter) {
      String name = modExp.application.tok.val;
      ModuleView mv = view.lookup(name);
      if (mv == null) {
        reporter.Error(MessageSource.Resolver, modExp.application.tok,
          $"Module Could not find module name {name}");
        return null;
      }

      ModuleView appliedView;

      // Apply actuals if offered
      if (modExp.application.moduleParams.Count > 0) {
        Contract.Assert(mv is DefModuleView);
        DefModuleView dmv = (DefModuleView) mv;

        List<ModuleView> actuals = new List<ModuleView>();
        foreach (ModuleExpression actualParam in modExp.application.moduleParams)
        {
          Console.Out.WriteLine("XXX-TODO check {actualParam} refines its formal");
          actuals.Add(ModuleView.resolveModuleExpression(view, actualParam, reporter));
        }

        if (dmv.Def.Formals.Count != actuals.Count)
        {
          reporter.Error(MessageSource.Resolver, modExp.application.tok,
              $"Module {dmv.Def.Name} expects {dmv.Def.Formals.Count} parameters, got {actuals.Count}");
          return null;
        }

        appliedView = new ApplicationModuleView(dmv, actuals);
      } else {
        if (mv is DefModuleView dmv) {
          Contract.Assert(dmv.Def.Formals.Count == 0); // XXX How do we feel if there are formals? Depends on the context...?
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

    public ModuleView lookup(string name) {
      ModuleView mv;
      if (Underway.TryGetValue(name, out mv)) {
        return mv;
      }
      if (RefinementView != null) {
        mv = RefinementView.lookup(name);
        if (mv != null) {
          return mv;
        }
      }
      if (Parent != null) {
        return Parent.lookup(name);
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

      // Add the formals first; they're reachable from the refinement expression.
      foreach (FormalModuleDecl formal in Def.Formals) {
        ModuleView fv = ModuleView.resolveModuleExpression(context, formal.ConstraintModExp, reporter);
        // There's no TopLevelDecl here, so we'll roll up an AliasModuleDecl to make
        // the type resolver happy.
        ModuleDecl fd = new AliasModuleDecl(formal.ConstraintModExp, formal.Name, Def, false, null);
        LocalViews.Add(formal.Name.val, new Tuple<ModuleDecl,ModuleView>(fd,fv));
        context.Add(formal.Name.val, fv);
      }

      // Evaluate the refinement expression next.
      RefinementView = null;
      if (Def.RefinementBaseModExp != null) {
        RefinementView = ModuleView.resolveModuleExpression(context, Def.RefinementBaseModExp, reporter);
        context.SetRefinementView(RefinementView);  // XXX and now RefinementView is, too.
      }

      // Then resolve the literals and evaluate the aliases inside the body.
      foreach (TopLevelDecl decl in Def.TopLevelDecls)
      {
        if (decl is LiteralModuleDecl lmd) {
          ModuleView lv = new DefModuleView(context, lmd.ModuleDef, lmd, reporter);
          LocalViews.Add(lmd.Name, new Tuple<ModuleDecl,ModuleView>(lmd,lv));
          context.Add(lmd.Name, lv);
        } else if (decl is AliasModuleDecl amd) {
          ModuleView lv = ModuleView.resolveModuleExpression(context, amd.TargetModExp, reporter);
          LocalViews.Add(amd.Name, new Tuple<ModuleDecl,ModuleView>(amd,lv));
          context.Add(amd.Name, lv);
        }
      }
      
      Console.Out.WriteLine($"new {this.ToString()}");
    }

    public override string ToString() {
      return $"Def({Def.Name})";
    }

    public ModuleView lookup(string name) {
      Tuple<ModuleDecl,ModuleView> lvt;
      if (LocalViews.TryGetValue(name, out lvt)) {
        return lvt.Item2;
      }
      if (RefinementView != null) {
        return RefinementView.lookup(name);
      }
      return null;
    }
    
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
  }

  public class ApplicationModuleView : ModuleView {
    internal readonly DefModuleView Prototype;
    internal readonly Dictionary<string, ModuleView> Substitutions;

    public ApplicationModuleView(DefModuleView prototype, List<ModuleView> actuals) {
      this.Prototype = prototype;
      this.Substitutions = new Dictionary<string, ModuleView>();
      Contract.Assert(prototype.Def.Formals.Count == actuals.Count);
      for (int i=0; i<prototype.Def.Formals.Count; i++) {
        Substitutions.Add(prototype.Def.Formals[i].Name.val, actuals[i]);
      }
      Console.Out.WriteLine($"new {this.ToString()}");
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder(100);
      sb.Append("Apply(").Append(Prototype).Append(":");
      for (int i=0; i<Prototype.Def.Formals.Count; i++) {
        if (i>0) { sb.Append(","); }
        string name = Prototype.Def.Formals[i].Name.val;
        sb.Append(name);
        sb.Append("=");
        sb.Append(Substitutions[name]);
      }
      sb.Append(")");
      return sb.ToString();
    }

    public ModuleView lookup(string name) {
      ModuleView mv;
      if (Substitutions.TryGetValue(name, out mv)) {
        return mv;
      }
      return Prototype.lookup(name);
    }

    public void BuildModuleVisitList(List<Tuple<ModuleDecl, ModuleView>> outModuleViews) {
      // All modules referenced by this application must already have been visited
    }

    public ModuleDecl GetDecl() {
      Contract.Assert(false); // unimpl! Do Travis' tricksy substitution.
      return null;
    }
  }
}
