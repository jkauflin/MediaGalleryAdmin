/*==============================================================================
 * (C) Copyright 2022,2023 John J Kauflin, All rights reserved.
 *----------------------------------------------------------------------------
 * DESCRIPTION: Client-side JS functions and logic for web app
 *----------------------------------------------------------------------------
 * Modification History
 * 2017-09-08 JJK 	Initial version
 * 2017-12-29 JJK	Initial controls and WebSocket communication
 * 2018-04-02 JJK   Added control to manually trigger watering
 * 2018-05-19 JJK   Added update of configuration store record values
 * 2018-06-18 JJK   Added lightDuration
 * 2018-08-19 JJK   Added description and dates
 * 2019-09-22 JJK   Getting it going again
 * 2019-09-28 JJK   Implemented modules concept and moved common methods to
 *                  util.js
 * 2022-04-17 JJK   Making updates for bootstrap 5, and to use fetch()
 *                  instead of AJAX.  Removed old websocket stuff
 * 2022-05-31 JJK   Updated to use newest fetch ideas for lookup and update,
 *                  and converted to use straight javascript
 * 2022-10-20 JJK   Re-implemented websocket connection to display async log
 * 2022-12-17 JJK   Re-implemented using .NET 6 C# backend server
 * 2023-02-05 JJK   Updated to Bootstrap v5.2, and other nav and menu
 *                  improvements from recent website changes
 *============================================================================*/
var main = (function () {
    'use strict';

    // Keep track of the state of the navbar collapse (shown or hidden)
    var navbarCollapseShown = false;
    var collapsibleNavbar = document.getElementsByClassName("navbar-collapse")[0];
    collapsibleNavbar.addEventListener('hidden.bs.collapse', function () {
        navbarCollapseShown = false;
    })
    collapsibleNavbar.addEventListener('shown.bs.collapse', function () {
        navbarCollapseShown = true;
    })

    // Listen for nav-link clicks
    document.querySelectorAll("a.nav-link").forEach(el => el.addEventListener("click", function (event) {
        // Automatically hide the navbar collapse when an item link is clicked (and the collapse is currently shown)
        if (navbarCollapseShown) {
            new bootstrap.Collapse(document.getElementsByClassName("navbar-collapse")[0]).hide();
        }
    }));

    //=================================================================================================================
    // Bind events
    document.getElementById("UpdFileInfoButton").addEventListener("click", _updFileInfo);
    document.getElementById("FileTransferButton").addEventListener("click", _fileTransfer);
    document.getElementById("AddMusicButton").addEventListener("click", _addMusicAlbum);
    
    //document.getElementById("EditFileInfoButton").addEventListener("click", _editFileInfo);


    //=================================================================================================================
    // Module methods
    function _updFileInfo(event) {
        document.getElementById("LogMessageDisplay").innerHTML = "Starting File Info update"
        _executeServerTask("UpdateFileInfo")
    }

    function _fileTransfer(event) {
        document.getElementById("LogMessageDisplay").innerHTML = "Starting File Transfer"
        _executeServerTask("FileTransfer")
    }

    function _addMusicAlbum(event) {
        document.getElementById("LogMessageDisplay").innerHTML = "Adding Music Album"
        _executeServerTask("AddMusicAlbum")
    }


    function _editFileInfo(event) {

        //let url = jjkgalleryRoot + "getMenuList.php"
        let url = "GetFileInfoList"
        //let urlParamStr = `?mediaType=${mediaType}`
        //fetch(url + urlParamStr)
        fetch(url)
            .then(response => response.json())
            .then(menuList => {
                //buildMenuElements(mediaType, MediaMenuId, menuList)
                //buildMenuElements(mediaType, MediaOffcanvasMenuId, menuList)
                console.log(">>> after the Fetch")
            });

    }

    /*
    function _update(event) {
        let url = 'UpdateConfig';
        fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: util.getParamDatafromInputs('InputValues')
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('Response was not OK');
            }
            return response.json();
        })
        .then(data => {
            document.getElementById("UpdateDisplay").innerHTML = "Update successful";
            //_renderConfig(data);
        })
        .catch((err) => {
            console.error(`Error in Fetch to ${url}, ${err}`);
            document.getElementById("UpdateDisplay").innerHTML = "Fetch data FAILED - check log";
        });
    }
    */

    // Try to establish a websocket connection with the server to execute a task
    function _executeServerTask(taskName) {
        var scheme = document.location.protocol === "https:" ? "wss" : "ws";
        var port = document.location.port ? (":" + document.location.port) : "";
        var connectionUrl = scheme + "://" + document.location.hostname + port + "/ws?taskName=" + taskName;
        var ws = new WebSocket(connectionUrl);
        //var ws = new WebSocket("ws://localhost:3045");

        // event emmited when connected
        ws.onopen = function () {
            //console.log('websocket is connected ...')
        }

        // event emmited when receiving message from the server (messages from the robot)
        ws.onmessage = function (messageEvent) {
            /*
            var serverMessage = JSON.parse(messageEvent.data);
            if (serverMessage.errorMessage != null) {
                logMessage(serverMessage.errorMessage);
            }
            */
            //console.log(">>> messageEvent.data = "+messageEvent.data)
            //document.getElementById("LogMessageDisplay").innerHTML += messageEvent.data + '<br>'
            var objDiv = document.getElementById("LogMessageDisplay")
            objDiv.innerHTML += '&#13;&#10;' + messageEvent.data;
            objDiv.scrollTop = objDiv.scrollHeight;
        } // on message (from server)

        ws.onclose = function () {
            //console.log("!!!!! onclose in the client")
        } // on message (from server)
    }

    function htmlEscape(str) {
        return str.toString()
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }


    //=================================================================================================================
    // This is what is exposed from this Module
    return {};

})(); // var main = (function(){
