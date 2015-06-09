using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Collections;
using ClawlerClassLibrary;
using System.Threading;
using System.IO;
using System.Web;

namespace ArticleApplication
{
    public partial class CrawlerForm : Form
    {
        public CrawlerForm()
        {
            InitializeComponent();
        }

        string duibiTime = "2015.06.08";
        Regex r = new Regex("<.*?>");
        CrawlerClass cClass = new CrawlerClass();
        //dbHelp db = new dbHelp("server=192.168.2.245;uid=a_dc;pwd=Aa123456;database=getNetworkData");
        dbHelp db = new dbHelp("server=192.168.2.245;uid=sa;pwd=3132_deeposh_0083;database=getNetworkData");
        List<WebInfo> wInfoList = new List<WebInfo>();
        ArrayList SQLStringList = new ArrayList();
        int keyWordsCount = 0;  //关键词总数
        int keyWordsCountDone = 0;  //已处理的关键词总数
        int cjCount = 0; //采集到的结果
        string webNameStr = "中国家电网;搜狗;新浪科技;物联网;新材料在线;千龙网;中国创新网;天极网;光明家电;techweb;万维家电"; //;百度学术
        string Kid = "";   //主题词ID
        string Did = "";   //纬度词ID
        DataTable dt_task = new DataTable();

        int dataTaskCount = 40;   //线程数量
        Thread getTaskThread = null;
        ManualResetEvent[] doneEvents;
        private static readonly object objLock = new object();
        private static readonly object objLockSql = new object();
        private static readonly object objLockLog = new object();

        private void CrawlerForm_Load(object sender, EventArgs e)
        {
            WebInfo wInfo = null;
            RequestHeaders rinfo = null;

            try
            {
                DataTable dt_param = db.selectDatas("SELECT * FROM getNetworkData..ArticleParam WHERE isqy=1");
                for (int i = 0; i < dt_param.Rows.Count; i++)
                {
                    wInfo = new WebInfo();
                    wInfo.WebName = dt_param.Rows[i]["WebName"].ToString();
                    wInfo.RkUrl = dt_param.Rows[i]["RkUrl"].ToString();
                    rinfo = new RequestHeaders();
                    rinfo.UserAgent = dt_param.Rows[i]["UserAgent"].ToString();
                    rinfo.Accept = dt_param.Rows[i]["Accept"].ToString();
                    rinfo.Referer = dt_param.Rows[i]["Referer"].ToString();
                    rinfo.Charset = Encoding.GetEncoding(dt_param.Rows[i]["Charset"].ToString());
                    wInfo.RequestInfo = rinfo;
                    wInfo.Pattern = dt_param.Rows[i]["Pattern"].ToString();
                    wInfo.PageNumPattern = dt_param.Rows[i]["PageNumPattern"].ToString();
                    int PageSize = 0;
                    if (int.TryParse(dt_param.Rows[i]["PageSize"].ToString(), out PageSize))
                    {
                        wInfo.PageSize = int.Parse(dt_param.Rows[i]["PageSize"].ToString());
                    }
                    wInfoList.Add(wInfo);
                }
            }
            catch(Exception ecp)
            {
                MessageBox.Show("异常：" + ecp.Message);
                return;
            }
            //duibiTime = DateTime.Now.AddDays(-1).ToString("yyyy.MM.dd");
            getTaskThread = new Thread(getTask);
            getTaskThread.Start();
        }

        /// <summary>
        /// 启动采集线程
        /// </summary>
        void getTask()
        {
            try
            {
                dt_task = db.selectDatas("SELECT A.Kid,B.Did,A.keyWord,B.keyWordDesc FROM getNetworkData..ArticleKeyWord A JOIN getNetworkData..ArticleKeyWordDesc B ON A.Kid=B.Kid WHERE A.isQY=1 AND B.isQY=1 ORDER BY Kid");
                if (dt_task.Rows.Count > 0)
                {
                    keyWordsCount = dt_task.Rows.Count;
                    doneEvents = new ManualResetEvent[dataTaskCount];
                    for (int i = 0; i < dataTaskCount; i++)
                    {
                        doneEvents[i] = new ManualResetEvent(false);
                        ThreadPool.QueueUserWorkItem(startCrawler, i);
                        Thread.Sleep(500);
                    }
                    WaitHandle.WaitAll(doneEvents);
                }
            }
            catch { }
            int gg = db.insertDatas("DELETE FROM getNetworkData..ArticleTable_刘大任 WHERE id NOT IN(SELECT MIN(id) FROM getNetworkData..ArticleTable_刘大任 GROUP BY SUBSTRING(内容摘要,CHARINDEX('。',内容摘要)-5,20) HAVING COUNT(0)>=1);DELETE FROM getNetworkData..ArticleTable_刘大任 WHERE id NOT IN(SELECT MIN(id) FROM getNetworkData..ArticleTable_刘大任 GROUP BY 新闻链接 HAVING COUNT(0)>=1);DELETE FROM getNetworkData..ArticleTable_刘大任 WHERE 访问数量!='' AND id NOT IN(SELECT MIN(id) FROM getNetworkData..ArticleTable_刘大任 GROUP BY 访问数量,标题 HAVING COUNT(0)>=1)");
            db.insertDatas("INSERT INTO getNetworkData..ArticelDoneTime SELECT GETDATE(),''");
            this.Invoke(new ThreadStart(delegate()
            {
                this.label2.Text = "采集完成 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }));
            Thread.Sleep(1000 * 60 * 60);
            this.Invoke(new ThreadStart(delegate() { this.Close(); }));
        }

        /// <summary>
        /// 开始采集 多线程
        /// </summary>
        /// <param name="taskIndex"></param>
        void startCrawler(object taskIndex)
        {
            while (true)
            {
                try
                {
                    DataTable dt_SearchKey = dt_task.Clone();
                    #region  获取要抓取的URL
                    lock (objLock)
                    {
                        try
                        {
                            if (dt_task.Rows.Count == 0)
                                break;
                            if (dt_task.Rows.Count >= 10)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    dt_SearchKey.ImportRow((DataRow)dt_task.Rows[0]);
                                    dt_task.Rows.Remove(dt_task.Rows[0]);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < dt_task.Rows.Count; i++)
                                {
                                    dt_SearchKey.ImportRow((DataRow)dt_task.Rows[0]);
                                    dt_task.Rows.Remove(dt_task.Rows[0]);
                                }
                            }
                        }
                        catch (Exception ecp)
                        {
                            writeLog(@"C:\Log_ArticleCrawler.log"
                                , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 从内存获取要关键字异常：" + ecp.Message + "\r\n");
                        }
                    }
                    #endregion

                    for (int j = 0; j < dt_SearchKey.Rows.Count; j++)
                    {
                        try
                        {
                            Kid = dt_SearchKey.Rows[j]["Kid"].ToString();
                            Did = dt_SearchKey.Rows[j]["Did"].ToString();
                            string keyWords = dt_SearchKey.Rows[j]["keyWord"].ToString() + " " + dt_SearchKey.Rows[j]["keyWordDesc"].ToString();
                            writeLog(@"C:\Log_ArticleCrawler_Done.log",
                                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Kid：[" + Kid + "] Did：[" + Did + "] keyWords：[" + keyWords + "]\r\n");
                            getArticleInfo(keyWords);
                            lock (objLockSql)
                            {
                                keyWordsCountDone += 1;
                                if (SQLStringList.Count > 0)
                                {
                                    if (db.ExecuteSqlTran(SQLStringList))
                                    {
                                        SQLStringList = new ArrayList();
                                    }
                                }
                                this.Invoke(new ThreadStart(delegate()
                                {
                                    this.label2.Text = "当前已处理关键词：[" + keyWordsCountDone + " / " + keyWordsCount + "]  共采集数据：" + cjCount + " 条";
                                }));
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            int threadIndex = Convert.ToInt32(taskIndex);
            doneEvents[threadIndex].Set();
        }

        /// <summary>
        /// 调用采集方法
        /// </summary>
        /// <param name="keyWordsObj"></param>
        private void getArticleInfo(string keyWordsObj)
        {
            string keyWords = keyWordsObj.ToString();
            List<ArticleInfo> aInfoList = new List<ArticleInfo>();

            string[] webNameList = webNameStr.Split(';');
            for (int i = 0; i < webNameList.Length; i++)
            {
                try
                {
                    getArticleList(keyWords, webNameList[i]);
                    lock (objLockSql)
                    {
                        if (SQLStringList.Count > 0)
                        {
                            if (db.ExecuteSqlTran(SQLStringList))
                            {
                                SQLStringList = new ArrayList();
                            }
                        }
                        this.Invoke(new ThreadStart(delegate()
                        {
                            this.label2.Text = "当前已处理关键词：[" + keyWordsCountDone + " / " + keyWordsCount + "]  共采集数据：" + cjCount + " 条";
                        }));
                    }
                }
                catch (Exception ecp)
                {
                    writeLog(@"C:\Log_ArticleCrawler.log",
                           DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 调用采集方法循环异常：" + ecp.Message + "\r\n");
                }
            }
        }

        /// <summary>
        /// 获取结果列表  -  采集方法
        /// </summary>
        /// <param name="keyWords">关键词</param>
        /// <param name="webName">网站</param>
        private void getArticleList(string keyWords, string webName)
        {
            WebInfo wInfo = wInfoList.Find(m => m.WebName == webName);
            if (wInfo != null)
            {
                string keyWordsTemp = keyWords;
                if (webName == "中国创新网" || webName == "万维家电")
                    keyWordsTemp = HttpUtility.UrlEncode(keyWords, Encoding.GetEncoding("GB2312"));
                string rkUrl = wInfo.RkUrl.Replace("[keyWords]", keyWordsTemp);
                int pageTotal = 2;
                bool flag = true;
                MatchCollection mcLast = null;
                for (int i = 1; i < pageTotal; i++)
                {
                    if (!flag)
                        break;

                    string url = "";
                    string m = "";
                    try
                    {
                        if (webName == "百度学术" || webName == "techweb")
                            url = rkUrl.Replace("{p}", ((i - 1) * 10).ToString());
                        else if (webName == "光明家电")
                            url = rkUrl.Replace("{p}", (i - 1).ToString());
                        else if (webName == "中国创新网" && i > 1)
                        {
                            Regex r_rkUrl = new Regex("(?<=href=\").*?(?=\" class=\"f1\">下一页)");
                            url = r_rkUrl.Match(m).Value.ToString().Trim();
                            if (url == "")
                                break;
                            else
                                url = "http://www.chinahightech.com/search/" + url;
                        }
                        else
                            url = rkUrl.Replace("{p}", i.ToString());
                        m = cClass.getHtmlCode(url, wInfo.RequestInfo);

                        #region 获取页码
                        if (wInfo.PageNumPattern != null && wInfo.PageNumPattern != "")
                        {
                            string pageNumText = Regex.Match(m, wInfo.PageNumPattern).Value.ToString();
                            pageNumText = pageNumText.Replace(",", "").Replace("，", "").Replace(" ", "").Trim();
                            int pageSize = wInfo.PageSize;
                            if (int.TryParse(pageNumText, out pageSize))
                            {
                                pageTotal = (int.Parse(pageNumText) / wInfo.PageSize);
                                if (pageTotal >= 10 && pageTotal < 50)
                                    pageTotal = pageTotal / 2 + 1;
                                else if (pageTotal >= 50)
                                    pageTotal = 11;
                            }
                            else
                                pageTotal = 2;
                        }
                        else
                            pageTotal += 1;
                        #endregion

                        MatchCollection mList = Regex.Matches(m, wInfo.Pattern);
                        if (mList != null && mList.Count > 0)
                        {
                            #region  正则取得结果
                            mcLast = cClass.cfBool(mList, mcLast);
                            if (mcLast == null)
                            {
                                break;
                            }
                            foreach (Match match in mList)
                            {
                                try
                                {
                                    //#region  正则取得结果
                                    ArticleInfo aInfo = new ArticleInfo();
                                    aInfo.Url = url;
                                    aInfo.KeyWords = keyWords;
                                    aInfo.WebName = wInfo.WebName;

                                    //第一财经日报 2015-05-19 01:55:52
                                    string sourceBy = match.Groups["sourceBy"].Value.ToString().Replace("\r", "").Replace("\n", "").Trim();
                                    sourceBy = r.Replace(sourceBy, "").Trim();

                                    #region 日期 判断
                                    if (wInfo.WebName == "新浪科技" || wInfo.WebName == "techweb")
                                    {
                                        string riqi = Regex.Match(sourceBy, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}").Value.ToString();
                                        try
                                        {
                                            aInfo.Riqi = Convert.ToDateTime(riqi.Substring(0, 10)).ToString("yyyy.MM.dd");
                                        }
                                        catch { }
                                        if (Convert.ToDateTime(aInfo.Riqi) < Convert.ToDateTime(duibiTime))
                                        {
                                            flag = false;
                                            break;
                                        }
                                        aInfo.SourceBy = sourceBy.Replace(riqi, "").Trim();
                                    }
                                    else
                                    {
                                        aInfo.Riqi = Convert.ToDateTime(match.Groups["riqi"].Value.ToString().Trim()).ToString("yyyy.MM.dd");
                                        aInfo.SourceBy = sourceBy;
                                        if (wInfo.WebName == "百度学术")
                                        {
                                            try
                                            {
                                                if (int.Parse(aInfo.Riqi) < int.Parse(duibiTime.Substring(0, 4)))
                                                {
                                                    flag = false;
                                                    break;
                                                }
                                            }
                                            catch
                                            {
                                                flag = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (!(wInfo.PageNumPattern != null && wInfo.PageNumPattern != ""))
                                            {
                                                if (Convert.ToDateTime(aInfo.Riqi) < Convert.ToDateTime(duibiTime))
                                                {
                                                    flag = false;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    #endregion

                                    string title = match.Groups["title"].Value.ToString();
                                    aInfo.Title = r.Replace(title, "").Trim();

                                    string content = match.Groups["content"].Value.ToString();
                                    aInfo.Content = r.Replace(content, "").Trim();

                                    if (containStr((aInfo.Title + aInfo.Content), aInfo.KeyWords) && !(aInfo.Title + aInfo.Content).Contains("车"))
                                    {

                                        if (Convert.ToDateTime(aInfo.Riqi) >= Convert.ToDateTime(duibiTime))
                                        {
                                            #region
                                            aInfo.Click = match.Groups["click"].Value.ToString().Trim();

                                            string sourceUrl = match.Groups["sourceUrl"].Value.ToString().Trim();
                                            aInfo.SourceUrl = r.Replace(sourceUrl, "").Trim();
                                            if (webName == "千龙网")
                                                aInfo.SourceUrl = "http://www.chinaso.com" + aInfo.SourceUrl;
                                            else if (webName == "中国创新网")
                                                aInfo.SourceUrl = "http://www.chinahightech.com/" + aInfo.SourceUrl;

                                            string insertSql = "INSERT INTO getNetworkData..ArticleTable_刘大任(Kid,Did,抓取网页,来源,访问数量,日期,标题,内容摘要,关键词,新闻链接,渠道) VALUES('" + Kid + "','" + Did + "','" + aInfo.Url + "','" + aInfo.SourceBy + "','" + aInfo.Click + "','" + aInfo.Riqi + "','" + aInfo.Title + "','" + aInfo.Content + "','" + keyWords + "','" + aInfo.SourceUrl + "','" + aInfo.WebName + "')";
                                            #endregion

                                            #region
                                            lock (objLockSql)
                                            {
                                                SQLStringList.Add(insertSql);
                                                cjCount += 1;
                                                if (SQLStringList.Count >= 200)
                                                {
                                                    if (db.ExecuteSqlTran(SQLStringList))
                                                    {
                                                        SQLStringList = new ArrayList();
                                                    }
                                                }
                                                this.Invoke(new ThreadStart(delegate()
                                                {
                                                    this.label2.Text = "当前已处理关键词：[" + keyWordsCountDone + " / " + keyWordsCount + "]  共采集数据：" + cjCount + " 条";
                                                }));
                                            }
                                            #endregion
                                        }
                                    }
                                }
                                catch (Exception ecp)
                                {
                                    writeLog(@"C:\Log_ArticleCrawler.log",
                                                       DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 采集方法异常：" + ecp.Message + " url=[" + url + "]\r\n");
                                }
                            }
                            #endregion
                        }
                        else
                            break;
                    }
                    catch (Exception ecp)
                    {
                        writeLog(@"C:\Log_ArticleCrawler.log",
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " 采集方法异常：" + ecp.Message + "\r\n");
                    }
                }
            }
        }

        /// <summary>
        ///  判断 m 是否 包含 key
        /// </summary>
        /// <param name="m"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        bool containStr(string m, string key)
        {
            bool flag = true;

            string[] keyS = key.Split(' ');
            for (int i = 0; i < keyS.Length; i++)
            {
                if (m.IndexOf(keyS[i].ToString()) == -1)
                {
                    flag = false;
                    break;
                }
            }

            return flag;
        }

        /// <summary>
        /// 日志
        /// </summary>
        /// <param name="logName">日志文件</param>
        /// <param name="mess">日志信息</param>
        void writeLog(string logName, string mess)
        {
            try
            {
                lock (objLockLog)
                {
                    File.AppendAllText(logName, mess);
                }
            }
            catch { }
        }

        private void CrawlerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (getTaskThread != null)
                getTaskThread.Abort();

            if (SQLStringList.Count > 0)
            {
                if (db.ExecuteSqlTran(SQLStringList))
                {
                    SQLStringList = new ArrayList();
                }
            }
        }
    }


    public class ArticleInfo
    {
        string url;

        public string Url
        {
            get { return url; }
            set
            {
                url = value;
                if (url.Length > 450)
                    url = url.Substring(0, 450);
            }
        }

        string click;

        public string Click
        {
            get { return click; }
            set { click = value; }
        }

        string sourceBy;

        public string SourceBy
        {
            get { return sourceBy; }
            set
            {
                sourceBy = value;
                if (sourceBy.Length > 80)
                    sourceBy = sourceBy.Substring(0, 80);
            }
        }

        string riqi;

        public string Riqi
        {
            get { return riqi; }
            set { riqi = value; }
        }

        string title;

        public string Title
        {
            get { return title; }
            set
            {
                title = value;
                title = title.Replace("nbsp;", "").Replace("\r", "").Replace("\n", "").Trim();
                if (title.Length > 450)
                    title = title.Substring(0, 450);
            }
        }

        string content;

        public string Content
        {
            get { return content; }
            set
            {
                content = value;
                content = content.Replace("nbsp;", "").Replace("\r", "").Replace("\n", "").Trim();
                if (content.Length > 950)
                    content = content.Substring(0, 950);
            }
        }

        string keyWords;

        public string KeyWords
        {
            get { return keyWords; }
            set { keyWords = value; }
        }

        string sourceUrl;

        public string SourceUrl
        {
            get { return sourceUrl; }
            set
            {
                sourceUrl = value;
                if (sourceUrl.Length > 450)
                    sourceUrl = sourceUrl.Substring(0, 450);
            }
        }

        string webName;

        public string WebName
        {
            get { return webName; }
            set { webName = value; }
        }
    }

    public class WebInfo
    {
        /// <summary>
        /// 网站
        /// </summary>
        string webName;

        public string WebName
        {
            get { return webName; }
            set { webName = value; }
        }

        string rkUrl;

        public string RkUrl
        {
            get { return rkUrl; }
            set { rkUrl = value; }
        }

        RequestHeaders requestInfo;

        public RequestHeaders RequestInfo
        {
            get { return requestInfo; }
            set { requestInfo = value; }
        }

        /// <summary>
        /// 正则表达式
        /// </summary>
        string pattern;

        public string Pattern
        {
            get { return pattern; }
            set { pattern = value; }
        }

        /// <summary>
        /// 排序
        /// </summary>
        string pageNumPattern;

        public string PageNumPattern
        {
            get { return pageNumPattern; }
            set { pageNumPattern = value; }
        }

        int pageSize;

        public int PageSize
        {
            get { return pageSize; }
            set { pageSize = value; }
        }
    }
}
