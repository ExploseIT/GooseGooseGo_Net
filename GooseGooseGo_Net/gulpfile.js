/**
 
 Install node and npm from https://nodejs.org

 Copy boostrap, jquery etc to the wwwroot/dist folder from node_modules by running 'npm install' then 'npx gulp'
 
 Remove initially installed Bootstrap, jQuery by removing the css, js, and lib folders from the wwwroot folder

 Remove also _ValidationScriptsPartial.cshtml in Views/Shared

 Update the Bootstrap and jQuery links to put to those generated in the wwwroot/dist folder

 In IIS
Open Authentication → Anonymous Authentication → Edit… → choose Application pool identity → OK.

In Command prompt
icacls "C:\Dev\GooseGooseGo_Dev\GooseGooseGo_Net\goosegoosego_net_data"/inheritance:e  /grant "IIS AppPool\ggg_net":(OI)(CI)RX /T


 **/

const path = require('path');
const wwwrootDist = 'wwwroot/dist'; 
const outputBase = wwwrootDist; //path.resolve(__dirname, '../../dist');
const merge = require('merge-stream');

const { src, dest, series } = require('gulp');
var clean = require('gulp-clean');
const fs = require('fs'); 

function bootstrapiconscopy() {
    return src('./node_modules/bootstrap-icons/**', { encoding: false })
        .pipe(dest(`${outputBase}/bootstrap-icons/`));
}

function jqueryuicopy() {
    const s1 = src('./node_modules/jquery-ui/dist/jquery-ui.min.js', { allowEmpty: true })
    .pipe(dest(`${outputBase}/jquery-ui/js/`));
  
    const s2 = src('./node_modules/jquery-ui/dist/themes/smoothness/*.css', { allowEmpty: true })
    .pipe(dest(`${outputBase}/jquery-ui/css/`));

    return merge(s1, s2);
}

 function bootstrapcopy() {
     const s1 = src('./node_modules/bootstrap/dist/css/bootstrap.min.*')
     .pipe(dest(`${outputBase}/bootstrap/css/`));
     const s2 = src('./node_modules/bootstrap/dist/js/bootstrap.bundle.min.*')
     .pipe(dest(`${outputBase}/bootstrap/js/`));
     return merge(s1, s2); 
 }
 
 function jquerycopy() {
     return src('./node_modules/jquery/dist/**')
   .pipe(dest(`${outputBase}/jquery/`));
 }


function fortawesomecopy() {
    const s1 = src('./node_modules/@fortawesome/fontawesome-free/css/all.css', { encoding: false })
        .pipe(dest(`${outputBase}/fontawesome-free/css/`));
    const s2 = src('./node_modules/@fortawesome/fontawesome-free/webfonts/fa-regular-400.*', { encoding: false })
        .pipe(dest(`${outputBase}/fontawesome-free/webfonts/`));
    const s3 = src('./node_modules/@fortawesome/fontawesome-free/webfonts/fa-solid-900.*', { encoding: false })
        .pipe(dest(`${outputBase}/fontawesome-free/webfonts/`));
    const s4 = src('./node_modules/@fortawesome/fontawesome-free/webfonts/fa-brands-400.*', { encoding: false })
        .pipe(dest(`${outputBase}/fontawesome-free/webfonts/`));
    return merge(s1, s2, s3, s4);
}
 
 function jqueryvalidationcopy() {
     return src('./node_modules/jquery-validation/dist/*.js')
         .pipe(dest(`${outputBase}/jquery-validation/`));
 }
 
 
 // The `clean` function is not exported so it can be considered a private task.
 // It can still be used within the `series()` composition.
function doclean() {
    const pathsToClean = [];

    const distPath = path.join(outputBase, '/');
    const wwwrootPath = path.join(wwwrootDist, '/');

    if (fs.existsSync(distPath)) {
        pathsToClean.push(distPath);
    } else {
        console.log(`Skipping clean: ${distPath} does not exist.`);
    }

    if (fs.existsSync(wwwrootPath)) {
        pathsToClean.push(wwwrootPath);
    } else {
        console.log(`Skipping clean: ${wwwrootPath} does not exist.`);
    }

    if (pathsToClean.length > 0) {
        return src(pathsToClean, { read: false, allowEmpty: true })
            .pipe(clean({ force: true }));
    } else {
        return Promise.resolve(); // nothing to clean
    }
}
   

exports.build = exports.default;

exports.default = series(
    doclean
    , bootstrapiconscopy
     , fortawesomecopy
     , bootstrapcopy
    , jquerycopy
    , jqueryvalidationcopy
    , jqueryuicopy
    
 );

 exports.doclean = series(
    doclean
 );

 exports.bootstrapiconscopy = bootstrapiconscopy;
 exports.jqueryuicopy = jqueryuicopy;
 exports.bootstrapcopy = bootstrapcopy;
 exports.jquerycopy = jquerycopy;
 exports.fortawesomecopy = fortawesomecopy;
 exports.jqueryvalidationcopy = jqueryvalidationcopy;


