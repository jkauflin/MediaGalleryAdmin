/*==============================================================================
 * (C) Copyright 2017,2022 John J Kauflin, All rights reserved.
 *----------------------------------------------------------------------------
 * DESCRIPTION:  Server-side Controller to handle websocket requests with
 *               a specified task to execute
 *----------------------------------------------------------------------------
 * Modification History
 * 2017-09-08 JJK 	Initial version
 * 2017-12-29 JJK	Initial controls and WebSocket communication
 * 2022-04-17 JJK   Making updates for bootstrap 5, and to use fetch()
 *                  instead of AJAX.  Removed old websocket stuff
 * 2022-05-31 JJK   Updated to use newest fetch ideas for lookup and update,
 *                  and converted to use straight javascript
 * 2022-10-20 JJK   Re-implemented websocket connection to display async log
 * 2022-12-17 JJK   Re-implemented using .NET 6 C# backend server instead of
 *                  nodejs
 *============================================================================*/
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace MediaGalleryAdmin.Controllers;

public class WebSocketController : ControllerBase
{
    [HttpGet("/ws")]
    public async Task Get(string taskName)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            ExecuteTask executeTask = new ExecuteTask(webSocket);
            if (taskName.Equals("FileTransfer"))
            {
                executeTask.FileTransfer();
            }

            // Close the websocket after completion
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

}
