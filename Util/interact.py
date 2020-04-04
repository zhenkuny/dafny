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


    def send_version_query(self):
        self.pipe.stdin.write('version\n'.encode(self.encoding))
        self.pipe.stdin.write((self.CLIENT_EOM_TAG + '\n').encode(self.encoding))
        self.pipe.stdin.flush()

    def send_verification_query(self): 
        self.pipe.stdin.write('verify\n'.encode(self.encoding))
        query = {"args" : [], "filename" : "Foo.cpp", "sourceIsFile" : True, "source" : "t.dfy"}
        j = json.dumps(query)
        print("JSON query: " + j)
        st = json.dumps(query) 
        b = st.encode(self.encoding)
        b64 = base64.b64encode(b) 
        #text = b64
        #text = (base64.b64encode() + '\n')
        #self.pipe.stdin.write(text.encode(self.encoding))
        self.pipe.stdin.write(b64)
        self.pipe.stdin.write(('\n' + self.CLIENT_EOM_TAG + '\n').encode(self.encoding))
        self.pipe.stdin.flush()

    def recv_response(self):
        response = None
        #response = json.loads(self.pipe.stdout.readline().decode(self.encoding))
        while True:
            response = self.pipe.stdout.readline().decode(self.encoding)
            
            if response.startswith("[%s] %s" % (self.SUCCESS, self.SERVER_EOM_TAG)):
                print("Ended in success")
                break
            elif response.startswith("[%s] %s" % (self.FAILURE, self.SERVER_EOM_TAG)):
                print("Ended in failure")
                break
            else:
                print(response)
#        while True:
#            response = json.loads(self.pipe.stdout.readline().decode(self.encoding))
#            if response['kind'] == 'response':
#                if response['status'] == 'success':
#                    break
#                else:
#                raise QueryError(response['response'])


def main():
    parser = argparse.ArgumentParser(description="Interact with the Dafny server")
    #parser.add_argument('--excel', action='store', help="Excel file to parse for accounts", required=False)
    args = parser.parse_args()

    server = DafnyServer('./Binaries/dafny-server')
    #sys.stdin.readline()
    #server.send_version_query()
    server.send_verification_query()
    server.recv_response()

    #event_loop()

if (__name__=="__main__"):
  main()


