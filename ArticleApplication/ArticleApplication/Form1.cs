using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ArticleApplication
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                WebService.Service1 wService = new WebService.Service1();
                DataTable dt_ArticelDoneTime = wService.selectDatas("SELECT * FROM getNetworkData..ArticelDoneTime WHERE ArticelTime>=CONVERT(VARCHAR,GETDATE(),23)");
                if (dt_ArticelDoneTime.Rows.Count == 0)
                {
                    if (MessageBox.Show("本次任务还未采集完成，是否继续查看采集结果？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.No)
                    {
                        this.label3.Text = "未查询";
                        dataGridView1.DataSource = new DataTable();
                        return;
                    }
                }
                DataTable dt = wService.selectDatas("SELECT * FROM getNetworkData..ArticleTable_刘大任 WHERE 日期>='" + dateTimePicker1.Value.ToString("yyyy.MM.dd") + "' AND 日期<'" + dateTimePicker2.Value.ToString("yyyy.MM.dd") + "'");
                dataGridView1.DataSource = dt;
                this.label3.Text = "共有：" + dt.Rows.Count;
            }
            catch (Exception ecp)
            {
                MessageBox.Show("异常：" + ecp.Message);
            }
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            dateTimePicker2.Value = dateTimePicker1.Value.AddDays(1);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dateTimePicker1.Value = DateTime.Now.AddDays(-1);
            //dateTimePicker1_ValueChanged(null, null);
        }
    }
}
