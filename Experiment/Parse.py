import ast, pprint
import sys

pp = pprint.PrettyPrinter(indent=2)

MAIN_NAME = "Main"

class Callable:
    def __init__(self, member):
        self.raw = member
        self.name = member['name']
        self.calls = []

    def add_trace(self, call):
        self.calls.append(call)

class Method(Callable): 
    def __init__(self, member):
        Callable.__init__(self, member)
        self.body = member['body']

class Lemma(Callable):
    def __init__(self, member):
        Callable.__init__(self, member)


class Function(Callable):
    def __init__(self, member):
        Callable.__init__(self, member)

class Parser:
    def _parse_main(self):
        main_method = self.methods[MAIN_NAME]
        stmts = main_method.body

        # the last one should be the call
        call_site = stmts[-1]

        if call_site['ntype'] == 'VarDeclStmt':
            assert len(call_site['update']['rhss']) == 1
            rhs = call_site['update']['rhss'][0]
            assert rhs['ntype'] == 'ExprRhs'
            call = rhs['expr']
            assert call['ntype'] == 'ApplySuffix'
            assert call['lhs']['name'] == self.target_name
            self.main_cs_id = call['nid']
        else:
            # pp.pprint(call_site)
            raise Exception("unhandled Main call site")

    def __init__(self, target_name) -> None:
        dfy_ast = open("ast.json").read()
        dfy_ast = ast.literal_eval(dfy_ast)

        self.methods = dict()
        self.lemmas = dict()
        self.functions = dict()
        self.target_name = target_name
        # main_method = None

        for member in dfy_ast:
            # print(member['ntype'])
            ntype = member['ntype']
            if ntype == "method":
                method = Method(member)
                self.methods[method.name] = method
            elif ntype == "lemma":
                lemma = Lemma(member)
                self.lemmas[lemma.name] = lemma
            elif ntype == "function":
                function = Function(member)
                self.functions[function.name] = function
            else:
                raise Exception("unhanled member " + ntype)

        if MAIN_NAME not in self.methods:
            raise Exception("Main method not found")

        if target_name not in self.methods:
            raise Exception("target method not found")

        self._parse_main()

    def _lookup_callee(self, name):
        if name in self.methods:
            return self.methods[name]

        if name in self.lemmas:
            return self.lemmas[name]

        if name in self.functions:
            return self.functions[name]
        
        raise Exception("callee not found")

    def _load_call(self, call):
        callee_name = call[1]
        callee = self._lookup_callee(callee_name)
        callee.add_trace(call)

    def load_trace(self, file_name):
        trace = open(file_name).readlines()

        for line in trace:
            line = line.strip()
            assert line[0] == "(" and line[-1] == ")"
            line = line[1:-1]
            line = line.split(", ")
            self._load_call(line)
        
        for method in self.methods.values():
            print(method.calls)

if __name__ == "__main__":
    p = Parser("dw_add")
    p.load_trace("Generated/out.trace")
