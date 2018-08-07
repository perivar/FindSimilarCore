// Write your JavaScript code.

function readUrl(input) {

    var files = input.files;
    var formData = new FormData();

    for (var i = 0; i != files.length; i++) {
        formData.append("files", files[i]);
    }

    // You can abort the upload by calling jqXhr.abort();   
    var jqXhr = $.ajax({
        url: "../api/files/upload",
        type: "POST",
        contentType: false,
        data: formData,
        dataType: "json",
        cache: false,
        processData: false,
        async: true,
        xhr: function () {
            var xhr = new window.XMLHttpRequest();
            xhr.upload.addEventListener("progress",
                function (e) {
                    if (e.lengthComputable) {
                        var percentage = Math.round((e.loaded / e.total) * 100);
                        input.setAttribute("data-title", percentage + '% uploaded');
                    }
                },
                false);
            return xhr;
        }
    })
        .done(function (data, textStatus, jqXhr) {
            input.setAttribute("data-title", 'Uploading successful');
        })
        .fail(function (jqXhr, textStatus, errorThrown) {
            if (errorThrown === "abort") {
                input.setAttribute("data-title", 'Uploading aborted!');
            } else {
                input.setAttribute("data-title", 'Uploading failed!');
            }
        })
        .always(function (data, textStatus, jqXhr) { });
}