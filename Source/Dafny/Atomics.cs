using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny.Linear
{
    internal class PreservedContents
    {
        public readonly String Atomic;
        public readonly String NewValue;

        public PreservedContents(String atomic, String newValue)
        {
            this.Atomic = atomic;
            this.NewValue = newValue;
        }
    }

    internal class BlockOpen
    {
        public readonly PreservedContents preservedContents;
        public readonly CallStmt stmt;

        public BlockOpen(PreservedContents preservedContents, CallStmt stmt)
        {
            this.preservedContents = preservedContents;
            this.stmt = stmt;
        }
    }
    
    public class AtomicRewriter : IRewriter
    {
        
        internal override void PostResolve(ModuleDefinition module)
        {
            foreach (var (_, method) in Visit.AllMethodMembers(module))
            {
                RewriteMethod(method);
            }
        }

        private const String EXECUTE_PREFIX = "execute__atomic__";
        private const String FINISH_NAME = "finish__atomic";
        private const String EXECUTE_NAME_NOOP = "execute__atomic__noop";

        private bool is_open_stmt(CallStmt call)
        {
            var name = call.Method.CompileName;
            var parts = name.Split(".");
            return parts[^1].StartsWith(EXECUTE_PREFIX);
        }

        private bool is_close_stmt(CallStmt call)
        {
            var name = call.Method.CompileName;
            var parts = name.Split(".");
            return parts[^1] == FINISH_NAME;
        }

        private bool is_open_call_entirely_ghost(CallStmt call)
        {
            var name = call.Method.CompileName;
            var parts = name.Split(".");
            return parts[^1] == EXECUTE_NAME_NOOP;
        }

        /*private void check_names_match(IToken tok, String s1, String s2)
        {
            var parts = s1.Split(".");
            Contract.Assert(parts[^1].StartsWith(EXECUTE_PREFIX));
            var newName = parts[^1].Substring(EXECUTE_PREFIX.Length);
            parts[^1] = FINISH_PREFIX + newName;
            var expectedCloseName = String.Join(".", parts);
            
            if (expectedCloseName != s2)
            {
                reporter.Error(MessageSource.Rewriter, tok,
                    "Improper atomic statement nesting: close and open don't match");
            }
        }*/

        private void check_open_close_match(
                    CallStmt openStmt,
                    CallStmt closeStmt,
                    PreservedContents openPreservedContents)
        {
            //var call1 = openStmt;
            //var call2 = closeStmt;
            //check_names_match(openStmt.Tok, call1.Method.CompileName, call2.Method.CompileName);

            PreservedContents closedPreservedContents = get_preserved_contents_close_stmt(closeStmt);

            if (openPreservedContents.Atomic != null
                && closedPreservedContents.Atomic != null
                && openPreservedContents.Atomic != closedPreservedContents.Atomic)
            {
                reporter.Error(MessageSource.Rewriter, closeStmt.Tok,
                    "Improper atomic statement nesting: 'atomic' field does not match: got '{0}' and '{1}'",
                    openPreservedContents.Atomic,
                    closedPreservedContents.Atomic);
            }
            
            if (openPreservedContents.NewValue != null
                && closedPreservedContents.NewValue != null
                && openPreservedContents.NewValue != closedPreservedContents.NewValue)
            {
                reporter.Error(MessageSource.Rewriter, closeStmt.Tok,
                    "Improper atomic statement nesting: 'new_value' field does not match: got '{0}' and '{1}'",
                    openPreservedContents.NewValue,
                    closedPreservedContents.NewValue);
            }
        }

        private String call_get_arg(CallStmt call, int i)
        {
            if (i >= call.Args.Count)
            {
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get arg of stmt: not enough args");
                return null;
            }
            else
            {
                var arg = call.Args[i].Expr;
                if (arg is NameSegment ns)
                {
                    if (ns.Resolved is IdentifierExpr id)
                    {
                        return id.Var.CompileName;
                    }
                }
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get arg of stmt: not an IdentifierExpr");
                return null;
            }
        }
        
        private String call_get_out_arg(CallStmt call, int i)
        {
            if (i >= call.Lhs.Count)
            {
                reporter.Error(MessageSource.Rewriter, call.Tok,
                    "Atomic checking: can't get out arg of stmt: not enough args");
                return null;
            }
            else
            {
                var arg = call.Lhs[i];
                if (arg is IdentifierExpr id)
                {
                    return id.Var.CompileName;
                }
                else
                {
                    reporter.Error(MessageSource.Rewriter, call.Tok,
                        "Atomic checking: can't get out arg of stmt: not an IdentifierExpr");
                    return null;
                }
            }
        }

        private PreservedContents get_preserved_contents_open_stmt(CallStmt openStmt)
        {
            return new PreservedContents(
                call_get_arg(openStmt, 0),
                call_get_out_arg(openStmt, 2));
        }
        
        private PreservedContents get_preserved_contents_close_stmt(CallStmt closeStmt)
        {
            return new PreservedContents(
                call_get_arg(closeStmt, 0),
                call_get_arg(closeStmt, 1));
        }

        private static CallStmt as_call_stmt(Statement stmt)
        {
            if (stmt is VarDeclStmt vds)
            {
                var cus = vds.Update;
                if (cus is UpdateStmt us)
                {
                    if (us.SubStatements.Count() == 1)
                    {
                        var sub = us.SubStatements.First();
                        if (sub is CallStmt cs)
                        {
                            return cs;
                        }
                    }
                }
            } else if (stmt is UpdateStmt us)
            {
                if (us.SubStatements.Count() == 1)
                {
                    var sub = us.SubStatements.First();
                    if (sub is CallStmt cs)
                    {
                        return cs;
                    }
                }
            } else if (stmt is CallStmt cs)
            {
                return cs;
            }

            return null;
        }

        private void check_no_assign_lhs(Expression lhs, String varName)
        {
            switch (lhs)
            {
                case NameSegment ns:
                    check_no_assign_lhs(ns.Resolved, varName);
                    break;
                case IdentifierExpr ie:
                    if (ie.Var.CompileName == varName)
                    {
                        reporter.Error(MessageSource.Rewriter, lhs.tok,
                            "Assign to variable which should be preserved for atomic block");
                    }

                    break;
                default:
                    Contract.Assert(false);
                    break;
            }
        }

        private void check_no_assign_stmt(Statement stmt, String varName)
        {
            if (stmt is UpdateStmt us)
            {
                foreach (Expression lhs in us.Lhss)
                {
                    check_no_assign_lhs(lhs, varName);
                }
            }
        }

        private void check_no_assign(Statement stmt, String varName)
        {
            check_no_assign_stmt(stmt, varName);
            foreach (var stmtList in Visit.AllStatementLists(stmt))
            {
                foreach (var s in stmtList)
                {
                    check_no_assign_stmt(s, varName);
                }
            }
        }

        public static bool IsGlinearStmt(Statement stmt)
        {
            var callStmt = as_call_stmt(stmt);
            if (callStmt != null)
            {
                var u = callStmt.Method.Usage;
                return ((u.IsLinearKind || u.IsSharedKind) && u.realm == LinearRealm.Erased && callStmt.Method.IsStatic);
            }

            if (stmt is VarDeclStmt vds)
            {
                return IsGlinearStmt(vds.Update);
            } else if (stmt is UpdateStmt us)
            {
                foreach (Statement sub in us.SubStatements)
                {
                    if (!IsGlinearStmt(sub))
                    {
                        return false;
                    }
                }

                return true;
            } else if (stmt is AssignStmt ast)
            {
                if (ast.Lhs is IdentifierExpr ie)
                {
                    var u = ie.Var.Usage;
                    return (u.IsLinearKind || u.IsSharedKind) && u.realm == LinearRealm.Erased;
                }

                return false;
            }

            return false;
        }

        private String get_id_of_identifier_call(Expression e)
        {
            if (e is ApplySuffix appsuff)
            {
                if (appsuff.ResolvedExpression is FunctionCallExpr fce)
                {
                    if (fce.Name == "identifier")
                    {
                        if (fce.Receiver is NameSegment ns)
                        {
                            if (ns.Resolved is IdentifierExpr ie)
                            {
                                return ie.Var.CompileName;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private bool is_correct_not_equals_assertion(Statement stmt, String expected_id1, String expected_id2)
        {
            Expression expr;
            if (stmt is AssertStmt assert_stmt)
            {
                expr = assert_stmt.Expr;
            }
            else if (stmt is AssumeStmt assume_stmt)
            {
                expr = assume_stmt.Expr;
            }
            else
            {
                return false;
            }

            if (expr is BinaryExpr be)
            {
                if (be.Op == BinaryExpr.Opcode.Neq)
                {
                    String actual_id1 = get_id_of_identifier_call(be.E0);
                    String actual_id2 = get_id_of_identifier_call(be.E1);
                    
                    Contract.Assert(expected_id1 != null);
                    Contract.Assert(expected_id2 != null);

                    return (actual_id1 == expected_id1 && actual_id2 == expected_id2)
                           || (actual_id1 == expected_id2 && actual_id2 == expected_id1);

                }
            }
            return false;
        }
        
        private void RewriteMethod(Method m) {
            var body = m.Body?.Body;
            if (body == null)
            {
                return;
            }
            foreach (var stmtList in Visit.AllStatementLists(body))
            {
                List<BlockOpen> openBlocks = new List<BlockOpen>();

                for (int stmtListIndex = 0; stmtListIndex < stmtList.Count; stmtListIndex++)
                {
                    var stmt = stmtList[stmtListIndex];

                    CallStmt call = as_call_stmt(stmt);
                    if (call != null && is_open_stmt(call))
                    {
                        if (openBlocks.Count > 0 && !is_open_call_entirely_ghost(call))
                        {
                            reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                "Improper atomic statement nesting: non-ghost open within another open");
                        }

                        var openStmt = call;
                        var preservedContents = get_preserved_contents_open_stmt(call);
                        
                        foreach (BlockOpen bo in openBlocks)
                        {
                            check_no_assign(openStmt, bo.preservedContents.Atomic);
                            check_no_assign(openStmt, bo.preservedContents.Atomic);
                        }
                        check_no_assign(openStmt, preservedContents.Atomic);

                        for (int j = 0; j < openBlocks.Count; j++)
                        {
                            int sIndex = stmtListIndex - openBlocks.Count + j;
                            if (!(sIndex >= 0 && is_correct_not_equals_assertion(stmtList[sIndex],
                                preservedContents.Atomic, openBlocks[j].preservedContents.Atomic)))
                            {
                                reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                    "Need to assert that ({0}.identifier() != {1}.identifier())",
                                    preservedContents.Atomic, openBlocks[j].preservedContents.Atomic);
                            }
                        }
                        
                        openBlocks.Add(new BlockOpen(preservedContents, openStmt));
                    }
                    else if (call != null && is_close_stmt(call))
                    {
                        if (openBlocks.Count == 0)
                        {
                            reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                "Improper atomic statement nesting: close without corresponding open");
                        }
                        else
                        {
                            var last = openBlocks[^ 1];
                            check_open_close_match(last.stmt, call, last.preservedContents);
                            openBlocks.RemoveAt(openBlocks.Count - 1);
                        }
                    }
                    else
                    {
                        if (openBlocks.Count > 0)
                        {
                            if (!stmt.IsGhost && !IsGlinearStmt(stmt))
                            {
                                reporter.Error(MessageSource.Rewriter, stmt.Tok,
                                    "Only ghost statements can be within an atomic block");
                            }
                            
                            foreach (BlockOpen bo in openBlocks) {
                                check_no_assign(stmt, bo.preservedContents.Atomic);
                                check_no_assign(stmt, bo.preservedContents.NewValue);
                            }
                        }
                    }
                }

                if (openBlocks.Count > 0)
                {
                    reporter.Error(MessageSource.Rewriter, m.Body.Tok,
                        "block ends with an atomic block open");
                }
            }
        }

        public AtomicRewriter(ErrorReporter reporter) : base(reporter)
        {
        }
    }
}