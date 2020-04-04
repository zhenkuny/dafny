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
#import prompt_toolkit as pt
#from prompt_toolkit.key_binding import KeyBindings
#from prompt_toolkit.validation import Validator

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
        print(query)
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
                print("Ended in success")
                break
            elif line.startswith("[%s] %s" % (self.FAILURE, self.SERVER_EOM_TAG)):
                print("Ended in failure")
                break
            else:
                response = response + line
        print(response)
        return response

    def parse_symbols(self, response):
        match = re.search("SYMBOLS_START (.*) SYMBOLS_END", response)
        if match:
            symbols = json.loads(match.group(1))
            print(symbols)
            return symbols
        else:
            print("Didn't find expected symbols in the server's response")
            return []


def find_functions_methods(symbols):
    names = []
    for sym in symbols:
        if sym['SymbolType'] == 'Method' or sym['SymbolType'] == 'Function':
            name = sym['Name']
            if not name == '_default':
                names.append(name)
    return names

def main():
    parser = argparse.ArgumentParser(description="Interact with the Dafny server")
    #parser.add_argument('--excel', action='store', help="Excel file to parse for accounts", required=False)
    args = parser.parse_args()

    server = DafnyServer('./Binaries/dafny-server')
    label = "Foo.cpp"
    task = Task([], label, True, "t.dfy")
    #sys.stdin.readline()
    #server.send_version_query()
    #server.send_verification_query(task)
    server.send_symbol_query(task)
    response = server.recv_response()
    symbols = server.parse_symbols(response)
    print(find_functions_methods(symbols))

    #event_loop()

if (__name__=="__main__"):
  main()


