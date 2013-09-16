// a simple async service wrapper around a command line tool that does something with some data from a database
// 
// basically, allows asynchronously run a config.executable_name for some xml data that is retrieved from 
// database using an integer id, get a "handle" in response and then to get result of the run using that
// "handle" passed to a separate http get

var express = require('express');
var util = require('util');
var odbc = require("odbc");
var uuid = require('node-uuid');
var db = new odbc.Database();
var fs = require('fs');
var spawn = require('child_process').spawn;
var config = require('./config');

db.open(config.connection_string, function(err)
{
    if (err) {
      console.log(err);
      process.exit(1);
    } 
});


var app = express();
 
app.get('/samples/:id', function(req, res) {
    db.query("select meta from Samples where sample_id = ?", [req.params.id], function(err, rows, moreResultSets)
    {
        res.header("Content-Type", "text/xml"); 
        res.send(rows[0].meta);
        util.debug(util.inspect(rows));
    });
});

app.get('/results/:handle', function(req, res) {
    var handle = req.params.handle;
    var file = config.tmp_path + handle;
    if (fs.existsSync(file)) {
      res.send({ complete: true, result: JSON.parse(fs.readFileSync(file)) });
    } else {
      res.send({ complete: false });
    }
});

app.post('/validate', function(req, res) {
    var body = '';
    req.on('data', function (data) {
        body += data;

        if (body.length > 20000) { 
            // FLOOD ATTACK OR FAULTY CLIENT, NUKE REQUEST
            req.connection.destroy();
        }
    });
    req.on('end', function () {
        var validator = spawn(
            config.executable_path + config.executable_name, 
            ['validate'],
            { cwd: config.executable_path }
        );
        util.inspect(validator);
        validator.stdout.setEncoding('utf8');
        validator.stderr.setEncoding('utf8');
        validator.stdin.setEncoding('utf8');
        var err = '', out = '';
        var handle = uuid.v4();
        var file = config.tmp_path + handle;
        validator.stdout.on('data', function (data) {
          util.inspect(data);
          out += data;
        });

        validator.stderr.on('data', function (data) {
          util.debug(data);
          err += data;
        });        

        validator.on('close', function (code) {
          util.debug(code);
          fs.writeFileSync(file, JSON.stringify({code: code, err: err, out: out}));
        });        

        validator.stdin.write(body);
        validator.stdin.end();
        res.send({handle: handle});
    });
});
 
app.listen(3000);
console.log('Listening on port 3000...');
