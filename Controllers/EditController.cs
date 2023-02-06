using Microsoft.AspNetCore.Mvc;
using MediaGalleryAdmin.Model;
using System.Diagnostics;
using System.Net.WebSockets;


namespace MediaGalleryAdmin.Controllers;

//namespace MediaGalleryAdmin.ControllerBase
//{
//[ApiController]
//[Route("api/[controller]")]
//[Route("api/cities")]
//[Route("[controller]")]
public class EditController : ControllerBase
{
    public IConfiguration _configuration;
    private readonly ILogger<EditController> _log;

    private static readonly Stopwatch timer = new Stopwatch();
    private static WebSocket? webSocket;
    private static string? dbConnStr;
    //private static System.Collections.Specialized.StringCollection log = new System.Collections.Specialized.StringCollection();
    private static DateTime lastRunDate;
    //private static ArrayList fileList = new ArrayList();
    //private static HttpClient httpClient = new HttpClient();

    private static Dictionary<string, string> configParamDict = new Dictionary<string, string>();
    private static List<DatePattern> dpList = new List<DatePattern>();


    public EditController(IConfiguration configuration, ILogger<EditController> logger)
    {
        _configuration = configuration;
        _log = logger;
        _log.LogDebug(1, "NLog injected into Controller");
        dbConnStr = _configuration["dbConnStr"];
    }

    // getFileList

    // GET: EditController
    // explictly adding a route (as opposed to letting the method name be the route)
    //[HttpGet("api/cities")]
    //[HttpGet]
    [HttpGet("/GetFileInfoList")]
    public JsonResult GetFileInfoList()
    {
        var fileInfo = new FileInfoTable();
        return new JsonResult(fileInfo);
    }

    /*
    [HttpGet({id})]

    // GET: EditController/Details/5
    public ActionResult Details(int id)
    {
        return View();
    }

    // GET: EditController/Create
    public ActionResult Create()
    {
        return View();
    }

    // POST: EditController/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Create(IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }

    // GET: EditController/Edit/5
    public ActionResult Edit(int id)
    {
        return View();
    }

    // POST: EditController/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Edit(int id, IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }

    // GET: EditController/Delete/5
    public ActionResult Delete(int id)
    {
        return View();
    }

    // POST: EditController/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Delete(int id, IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }
    */
}

//}
