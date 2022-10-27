/*==============================================================================
 * (C) Copyright 2017,2019,2021,2022 John J Kauflin, All rights reserved.
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
 *============================================================================*/
var main = (function () {
    'use strict';

    //=================================================================================================================
    // Bind events
    //document.getElementById("ClearLogButton").addEventListener("click", _clearLog);
    document.getElementById("UpdateButton").addEventListener("click", _update);
    document.getElementById("FileTransferButton").addEventListener("click", _fileTransfer);

    //=================================================================================================================
    // Module methods
    function _fileTransfer(event) {
        document.getElementById("LogMessageDisplay").innerHTML = ""
        fetch('FileTransfer')
    }

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

    // General function to send the botMessageStr to the server if Websocket is connected
    /*
    function sendCommand(botMessageStr) {
        //console.log("in sendCommand, wsConnected = "+wsConnected);
        if (wsConnected) {
            console.log(">>> sendCommand, botMessage = "+botMessageStr);
            ws.send(botMessageStr);
        }
    }
    */
    // Try to establish a websocket connection with the server
    _connectToServer();
    function _connectToServer() {
        var ws = new WebSocket("ws://localhost:3045");
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
            objDiv.innerHTML += messageEvent.data + '&#13;&#10;'
            objDiv.scrollTop = objDiv.scrollHeight;
        } // on message (from server)

        ws.onclose = function () {
            //console.log("on close in the client")
        } // on message (from server)
    }

    //=================================================================================================================
    // This is what is exposed from this Module
    return {};

})(); // var main = (function(){
