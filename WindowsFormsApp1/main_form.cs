using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace WindowsFormsApp1
{

    public partial class main_form : Form
    {
        string Today = DateTime.Now.ToShortDateString().Replace("/", "-");
        private string receiveData = "";
        translate_form t_f;
        view v_f;

        //flag
        //0:(New Lesson)
        //1:video view
        public static int flag = 0;
        public static string video_path = @"C:\cs_source\CL_source\2020-11-30\";

        public main_form()
        {
            InitializeComponent();

            //今日の日付
            Console.WriteLine("Today:"+Today);

        }

        private void monthCalendar1_DateSelected(object sender, DateRangeEventArgs e)
        {
            //呼び出しの確認
            Console.Write("monthCalendar1_DateSelected\n");

            //日付(date)の取得
            DateTime start = e.Start;
            DateTime end = e.End;
            Console.WriteLine("Selected: " + start.ToShortDateString());
            string date = start.ToShortDateString().Replace("/", "-");
            Console.WriteLine("date:" + date);

            //listBox1のitemの全消去
            listBox1.Items.Clear();

            //listboxの選択が解除されるのでスタートボタンを向こうにする
            if (button1.Enabled == true) //有効になっていたら
            {
                button1.Enabled = false; //無効にする
            }

            //dateにファイルが保存されているか確認
            if (SafeCreateDirectory(@"C:\cs_source\CL_source\"+date) == 1)
            {
                string[] file_paths = Directory.GetFiles(@"C:\cs_source\CL_source\"+date, "*",SearchOption.TopDirectoryOnly);
                foreach(string path in file_paths)
                {
                    //ファイル名(授業の保存ファイル)の取得
                    string file_name = Path.GetFileNameWithoutExtension(path);
                    Console.WriteLine(file_name);

                    if(file_name == "video")
                    {
                        listBox1.Items.Add(file_name);
                    }

                }

            }

            //今日の日付が選択されている場合はlistbox1に"(new lesson)"を追加する
            if(date == Today)
            {
                listBox1.Items.Add("(New Lesson)");
            }

        }

        public string ReceiveData
        {
            set
            {
                receiveData = value;
                //textBox1.Text = receiveData;
            }
            get
            {
                return receiveData;
            }
        }

        // translate_formに遷移
        private void button1_Click(object sender, EventArgs e)
        {
            //次画面を非表示
            //this.Visible = false;

            //選択されている項目のテキストを表示する
            Console.WriteLine("Text:{0}", listBox1.Text);

            if (listBox1.Text=="(New Lesson)")
            {
                Console.WriteLine("(New Lesson) Start pushed");
                flag = 0; //New Lesson

                //CL_sourceにフォルダを作成

                //保存ファイルの名前をユーザーに入力させる

                //aviファイルとwavファイルの保存先を指定

                //translate_formを表示
                t_f = new translate_form();
                t_f.ShowDialog();

            }
            else
            {
                Console.WriteLine("View Start pushed");
                flag = 1; //view

                //開くaviファイルとwavファイルのパスを指定してtranslate_formに渡す

                //translate_formを表示
                v_f = new view();
                v_f.ShowDialog();

            }

            //translate_formを表示
            //t_f = new translate_form();
            //t_f.ShowDialog();

        }

        //ディレクトリの確認
        private int SafeCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Console.WriteLine("Directory Exist\n");
                return 1; //ディレクトリが存在した場合
            }
            else
            {
                Console.WriteLine("not Directory Exist\n");
                //Directory.CreateDirectory(path);
                return 0; //ディレクトリが存在しない場合
            }
            
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //呼び出しの確認
            Console.WriteLine("listBox1_SelectedIndexChanged\n");

            if (button1.Enabled == false) //無効になっていたら
            {
                button1.Enabled = true; //有効にする
            }
        }

        private void main_form_Load(object sender, EventArgs e)
        {

        }
    }
}
