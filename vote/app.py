import os
import socket
import random
import json

from flask import Flask, render_template, request, make_response, g
from redis import Redis

app = Flask(__name__)

options = os.getenv(
    'VOTING_OPTIONS',
    'Python,Java,JavaScript,C++,Go,Rust,C#,C'
).split(',')
hostname = socket.gethostname()


def get_redis():
    if not hasattr(g, 'redis'):
        g.redis = Redis(host=os.getenv('REDIS_HOST', 'localhost'), db=0, socket_timeout=5)
    return g.redis


@app.route("/", methods=['POST', 'GET'])
def hello():
    voter_id = request.cookies.get('voter_id')
    if not voter_id:
        voter_id = hex(random.getrandbits(64))[2:-1]

    vote = None

    if request.method == 'POST':
        redis = get_redis()
        vote = request.form['vote']
        data = json.dumps({'voter_id': voter_id, 'vote': vote})
        redis.rpush('votes', data)

    resp = make_response(render_template(
        'index.html',
        options=options,
        hostname=hostname,
        vote=vote,
    ))
    resp.set_cookie('voter_id', voter_id)
    return resp


if __name__ == "__main__":
    app.run(host='0.0.0.0', port=5000, debug=True, threaded=True)