import ast
import pprint

f = open("ast.json")
s = f.read()

a = ast.literal_eval(s)

pp = pprint.PrettyPrinter(indent=2)

pp.pprint(a)
