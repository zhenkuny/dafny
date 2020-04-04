#!/usr/local/bin/python3

# Script for running Dafny in interactive mode

import argparse
import time
import re
import datetime
import sys
import subprocess
import json
import os
import base64
import prompt_toolkit as pt # Install via:  pip3 install prompt_toolkit
from prompt_toolkit.key_binding import KeyBindings
from prompt_toolkit.validation import Validator

class Task:
    def __init__(self, args, file_label, sourceIsFile, file_name):
        self.args = args
        self.file_label = file_label
        self.sourceIsFile = sourceIsFile
        self.file_name = file_name

    def to_dict(self):
        return {"args" :         self.args, 
                "filename" :     self.file_label,
                "sourceIsFile" : self.sourceIsFile,
                "source" :       self.file_name}

class DafnyServer:
    def __init__(self, server_path):
        self.encoding = 'utf-8'
        self.SUCCESS = "SUCCESS"
        self.FAILURE = "FAILURE"
        self.SERVER_EOM_TAG = "[[DAFNY-SERVER: EOM]]"
        self.CLIENT_EOM_TAG = "[[DAFNY-CLIENT: EOM]]"
        try:
            self.pipe = subprocess.Popen(server_path, 
                                         stdin = subprocess.PIPE, 
                                         stdout = subprocess.PIPE, 
                                         stderr = subprocess.STDOUT)
        except subprocess.CalledProcessError as e:
            print(f'Error starting the DafnyServer: {e.output}')
            exit()


    def write(self, a_string):
        self.pipe.stdin.write((a_string + '\n').encode(self.encoding))

    def send_version_query(self):
        self.write('version')
        self.write(self.CLIENT_EOM_TAG)
        self.pipe.stdin.flush()

    def write_verification_task(self, task): 
        query = task.to_dict()
        #print(query)
        j_string = json.dumps(query) 
        b = j_string.encode(self.encoding) # Convert to bytes
        b64 = base64.b64encode(b)          # Produce base64-encoded bytes
        self.pipe.stdin.write(b64)
        self.write('')                     # Add a newline

    def send_symbol_query(self, task):
        self.write('symbols')
        self.write_verification_task(task)
        self.write(self.CLIENT_EOM_TAG)
        self.pipe.stdin.flush()

    def send_verification_query(self, task):
        self.write('verify')
        self.write_verification_task(task)
        self.write(self.CLIENT_EOM_TAG)
        self.pipe.stdin.flush()

    def recv_response(self):
        response = ""
        while True:
            line = self.pipe.stdout.readline().decode(self.encoding)
            
            if line.startswith("[%s] %s" % (self.SUCCESS, self.SERVER_EOM_TAG)):
                #print("Ended in success")
                break
            elif line.startswith("[%s] %s" % (self.FAILURE, self.SERVER_EOM_TAG)):
                print("WARNING: Server operation ended in failure")
                break
            elif line.startswith("Verification completed successfully!"):
                pass # Suppress this unhelpful value
            else:
                response = response + line
        #print(response)
        return response

    def parse_symbols(self, response):
        match = re.search("SYMBOLS_START (.*) SYMBOLS_END", response)
        if match:
            symbols = json.loads(match.group(1))
            #print(symbols)
            return symbols
        else:
            print("Didn't find expected symbols in the server's response")
            return []

    def find_functions_methods(self, symbols):
        names = []
        for sym in symbols:
            if sym['SymbolType'] == 'Method' or sym['SymbolType'] == 'Function':
                name = sym['Name']
                if not name == '_default':
                    names.append(name)
        return names

    def get_version(self):
        self.send_version_query()
        response = self.recv_response()
        return response

    def get_functions_methods(self, task):
        self.send_symbol_query(task)
        response = self.recv_response()
        symbols = self.parse_symbols(response)
        return self.find_functions_methods(symbols)

    def do_verification(self, task):
        self.send_verification_query(task)
        response = self.recv_response()
        return response

def parse_args(args):
    a = args.split(' ')
    #print("Using Dafny arguments: %s" % a)
    return a

def read_arg_file(file_name):
    with open(file_name, 'r') as arg_file:
        arg_line = arg_file.readline()
        return parse_args(arg_line)


#############################################
#
#   UI
#
#############################################

bindings = KeyBindings()
@bindings.add('escape')
def _(event):
    event.app.exit(exception=EOFError)

def is_number(text):
    return text.isdigit()

def in_bounds(n, lbound=None, ubound=None):
    return (lbound is None or lbound <= int(n)) and (ubound is None or int(n) < ubound)

def do_file(session, data):
    server, dfy_args, dfy_file_name = data
    task = Task(dfy_args, dfy_file_name, True, dfy_file_name)
    print(server.do_verification(task))

def do_function_method(session, data):
    server, dfy_args, dfy_file_name = data
    task = Task(dfy_args, dfy_file_name, True, dfy_file_name)
    names = server.get_functions_methods(task)
    print("\nFound:")
    for name in names:
        print("\t" + name)
    #names = sorted(names)
    name_completer = pt.completion.WordCompleter(names, ignore_case=True)
    name = session.prompt("Enter a name (tab complete at any time): ",
                          completer=name_completer,
                          complete_while_typing=True,
                          validate_while_typing=False,
                          validator=Validators.set_validator(set(names)))
    args = dfy_args + ["/proc:*%s*" % name]
    task = Task(args, dfy_file_name, True, dfy_file_name)
    print(server.do_verification(task))

class Validators:
    @staticmethod
    def number_validator(lbound=None, ubound=None):
        return Validator.from_callable(
                lambda s : is_number(s) and in_bounds(s, lbound, ubound),
                error_message='This input may only contain numeric characters' 
                             + ('' if lbound is None else ' and it must be >= %d' % lbound)
                             + ('' if ubound is None else ' and it must be < %d' % ubound),
                move_cursor_to_end=True)

    @staticmethod
    def set_validator(s):
        options_str = ', '.join(sorted(list(s)))
        return Validator.from_callable(
            lambda i : i in s,
            error_message = "Sorry, that's not a valid option.  Try one of these: " 
                          + options_str
                          + ".  Tab complete may help!",
            move_cursor_to_end=True)

def dispatcher(session, options, data):
    print("Please choose from the following options: ")
    for (index, (option, func)) in enumerate(options):
        print("\t%d) %s" % (index, option))

    selection = int(session.prompt('Option: ', 
                                   validate_while_typing=False,
                                   validator=Validators.number_validator(0, len(options))))
    _, func = options[selection]
    func(session, data)

def event_loop(server, dfy_args, dfy_file_name):
    our_history = pt.history.FileHistory(".cmd_history")
    session = pt.PromptSession(history=our_history, key_bindings=bindings)
    actions = [('Verify the File', do_file),
               ('Verify one Method/Function',do_function_method)] 
    while True:
        try: 
            dispatcher(session, actions, (server, dfy_args, dfy_file_name))
        except EOFError:
            break
        else:
            pass



def main():
    default_arg_file_name = 'dfy.args'
    default_server_path = './Binaries/dafny-server'
    parser = argparse.ArgumentParser(description="Interact with the Dafny server")
    parser.add_argument('-d', '--dfy', action='store', help="Dafny file to verify", required=True)
    parser.add_argument('-a', '--args', action='store', help="Dafny arguments.  Overrides --arg_file", required=False)
    arg_file_help  = "File to read Dafny arguments from."
    arg_file_help += "Should consist of one line with all of the desired command-line arguments." 
    arg_file_help += "Defaults to %s" % default_arg_file_name
    parser.add_argument('-f', '--arg_file', action='store', default=default_arg_file_name,
                        required=False, help=arg_file_help)
    parser.add_argument('-s', '--server', action='store', default=default_server_path, required=False,
                        help="Path to the DafnyServer.  Defaults to %s" % default_server_path)
    
    args = parser.parse_args()

    server = DafnyServer(args.server)

    dfy_args = []
    if not args.args is None:
        dfy_args = parse_args(args.args)
    elif os.path.isfile(args.arg_file):
        dfy_args = read_arg_file(args.arg_file)

    event_loop(server, dfy_args, args.dfy)

#    label = args.dfy
#    task = Task(dfy_args, label, True, args.dfy)
    #print(server.get_version())
    #print(server.get_functions_methods(task))
    print(server.do_verification(task))
    #sys.stdin.readline()
    #server.send_version_query()
    #server.send_verification_query(task)
#    server.send_symbol_query(task)
#    response = server.recv_response()
#    symbols = server.parse_symbols(response)
#    print(find_functions_methods(symbols))

    #event_loop()

if (__name__=="__main__"):
  main()


