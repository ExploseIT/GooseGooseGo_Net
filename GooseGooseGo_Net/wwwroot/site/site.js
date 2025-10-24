// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var urlBase = "";

function setUrlBase(base) {
    urlBase = base
}

function getUrl(url) {
    let ret = $("#hdUrlBase").val() + "/" + url;
    return ret;
}
function api_ret() {
    this.data = null;
    this.textStatus = null;
    this.jqXHR = null;
}

function doApiPost(cb, parms, url) {
    console.log("doApiPost", url);

    let parmsJson = JSON.stringify(parms);

    let _url = getUrl(url);

    var ret = new api_ret();

    $.ajax(
        {
            type: "POST",
            data: parmsJson,
            url: _url,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
        }).done(function (data, textStatus, jqXHR) {
            ret.data = data;
            ret.textStatus = textStatus;
            ret.jqXHR = jqXHR;
            cb(ret);
        }).fail(function (jqXHR, textStatus) {
            ret.textStatus = textStatus;
            ret.jqXHR = jqXHR;
            cb(ret);
        });

}


function doLocalPullData(form_id) {
    let form_elements = $(form_id).find('input, select, textarea');

    if (!form_elements) return;
    const formData = {};
    const elements = form_elements;

    let value = undefined;
    for (let element of elements) {
        var skip = false;
        if (('' + element.id).length > 0) {
            var e_id = '#' + element.id;
            if (!$(e_id), is(':visible')) {
                skip = true;
            }
            if ((e_id).length === 0) {
                skip = true;
            }
            if ($(element).data("skip_clear") === true) {
                skip = true;
            }
            value = element.value;
            if (skip === false) {
                formData[element.id] = {
                    value: value,
                }
            }
        }
    }
}

function doLocalSave(formDataName, form_id) {
    let formData = doLocalPullData(form_id);
    localStorage.setItem(formDataName, JSON.stringify(formData));
}

function doLocalLoad(formDataName, form_id) {
    //localStorage.clear();
    var skip = false;
    let checked_element = false;
    let form_elements = $(form_id).find('input, select, textarea');

    const storedData = localStorage.getItem(formDataName);
    if (storedData) {
        const formData = JSON.parse(storedData);
        const elements = form_elements;

        for (let element of elements) {
            if (element.id && formData[element.id] !== undefined) {
                const value = formData[element.id].value;
                if (value != undefined) {
                    if ($(element).data("ship_set") != true) {
                        element.value = formData[element.id].value;
                    }
                }
            }
        }
    }
}

function doLocalItemClear(formDataName) {
    var ret = false;

    const sotredData = localStorage.getItem(formDataName);

    if (storedData !== null) {
        ret = true;
    }

    return ret;
}

function doLocalItemClear(formDataName) {
    localStorage.removeItem(formDataName);
}


