using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Google.Cloud.Vision.V1;
using NAudio.Wave;
using NAudio.Codecs;
using NAudio.CoreAudioApi;
using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading;
using Python.Runtime;
using System.IO;

namespace WindowsFormsApp1
{
    public partial class view : Form
    {
        int WIDTH = 640;
        int HEIGHT = 480;
        System.Drawing.Point MD = new System.Drawing.Point();
        System.Drawing.Point MU = new System.Drawing.Point();
        bool view_flag = false;
        Mat frame; //描画用
        Mat img;
        Mat frame_movie; //録画用
        VideoCapture capture;
        Bitmap bmp;
        Bitmap bmp_init;
        Graphics graphic;
        //bool backgroundWorker1_cancel_flag = false;

        int sqr_width = 0;  // square 幅
        int sqr_height = 0; // square 高さ

        static string view_text;
        static string view_voice_text;
        static string view_text_result;
        static string view_voice_text_result;

        // 音声再生用
        WaveOut waveout = new WaveOut();
        AudioFileReader waveReader;

        public view()
        {
            InitializeComponent();

            Console.Write("translate_form\n");

            // アクセストークンの取得
            Test.view_api_token();

            // テキストボックスに垂直スクロールバーを表示する
            //this.richTextBox1.ScrollBars = ScrollBars.Vertical;
            // ウインドウのサイズを固定
            this.FormBorderStyle = FormBorderStyle.FixedSingle;


            //カメラ画像取得用のVideoCapture作成
            //capture = new VideoCapture(0);
            capture = new VideoCapture(main_form.video_path + "video.avi");

            //capture.FrameWidth = WIDTH;
            //capture.FrameHeight = HEIGHT;

            //取得先のMat作成
            frame = new Mat(capture.FrameHeight, capture.FrameWidth, MatType.CV_8UC3);
            Console.WriteLine(capture.FrameWidth);
            Console.WriteLine(capture.FrameHeight);

            //表示用のBitmap作成
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);

            Console.WriteLine(frame.Cols);
            Console.WriteLine(frame.Rows);

            Console.Write("FPS::");
            Console.WriteLine(capture.Fps);

            //PictureBoxを出力サイズに合わせる
            pictureBox1.Width = frame.Cols;
            pictureBox1.Height = frame.Rows;

            //音声ファイルの読み込み
            waveReader = new AudioFileReader(main_form.video_path + "audio.wav");
            waveout.Init(waveReader);

            // 音声ファイルの再生
            waveout.Play();

            //描画用のGraphics作成
            graphic = pictureBox1.CreateGraphics();

            //画像取得スレッド開始
            backgroundWorker1.RunWorkerAsync();

            // 音声翻訳スレッド開始
            backgroundWorker2.RunWorkerAsync();

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Console.Write("backgroundWorker1_DoWorker\n");

            BackgroundWorker bw = (BackgroundWorker)sender;

            // Stopwatchクラス生成
            var sw = new System.Diagnostics.Stopwatch();

            Thread.Sleep(1000);

            sw.Start();

            while (!backgroundWorker1.CancellationPending)
            {
                //画像取得
                capture.Grab();
                NativeMethods.videoio_VideoCapture_operatorRightShift_Mat(capture.CvPtr, frame.CvPtr);

                bw.ReportProgress(0);

                //Thread.Sleep((int)(1000 / capture.Fps));
                Thread.Sleep((int)(1000 *(70/4.898)*(27/21.2) / capture.Fps)); //このときにaviファイルとwavファイルの再生速度が一致する

            }

            sw.Stop();

            // 結果表示
            Console.WriteLine("■処理Aにかかった時間");
            TimeSpan ts = sw.Elapsed;
            Console.WriteLine($"　{ts}");
            Console.WriteLine($"　{ts.Hours}時間 {ts.Minutes}分 {ts.Seconds}秒 {ts.Milliseconds}ミリ秒");
            Console.WriteLine($"　{sw.ElapsedMilliseconds}ミリ秒");

        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //描画
            graphic.DrawImage(bmp, 0, 0, frame.Cols, frame.Rows);
        }

        private void view_FormClosing(object sender, FormClosingEventArgs e)
        {
            Console.Write("Form1_FormClosing\n");

            //スレッドの終了を待機
            backgroundWorker1.CancelAsync();
            while (backgroundWorker1.IsBusy)
                Application.DoEvents();

            // 音声ファイルの停止
            waveout.Stop();

        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseDown\n");

            if (view_flag == false)
            {
                backgroundWorker1.CancelAsync();
                bmp_init = bmp;
                img = BitmapConverter.ToMat(bmp_init);

                //描画フラグON
                view_flag = true;

                waveout.Pause();

            }

            // Mouseを押した座標を記録
            MD.X = e.X;
            MD.Y = e.Y;


        }

        async private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseUp\n");

            System.Drawing.Point start = new System.Drawing.Point();
            System.Drawing.Point end = new System.Drawing.Point();

            // Mouseを離した座標を記録
            MU.X = e.X;
            MU.Y = e.Y;

            // 画像のトリミング
            //GetRegion(startX, startY, endX, endY);
            // 座標から(X,Y)座標を計算
            GetRegion(MD, MU, ref start, ref end);
            sqr_width = GetLength(start.X, end.X);
            sqr_height = GetLength(start.Y, end.Y);

            //Cv2.ImShow("test1", frame.Clone(new Rect(start.X, start.Y, sqr_width, sqr_height)));
            if ((sqr_width > 0) && (sqr_height > 0))
            {
                Console.WriteLine("startX:" + start.X.ToString() + ",sqr_width:" + sqr_width.ToString() + ",startY:" + start.Y.ToString() + ",sqr_height:" + sqr_height.ToString());
                frame.Clone(new Rect(start.X, start.Y, sqr_width, sqr_height)).SaveImage(@"C:\cs_source\img\cap.png");

                // 文字の認識
                string filePath = @"C:\cs_source\img\cap.png";
                view_text = RunOCR(filePath);
                Console.WriteLine("text:" + view_text);

                //文字の翻訳
                await Test.view_recognize_api();

                //テキストボックスに文字を追加する
                if (view_text_result != "" || view_text != "")
                {
                    richTextBox1.AppendText(view_text + "\n");
                    richTextBox1.AppendText(view_text_result + "\n");
                    richTextBox1.AppendText("---------------------------------------------------\n");
                }

                view_text = "";
                view_text_result = "";

            }
            /* 確かめ
            using (Mat cap = new Mat(@"C:\cs_source\img\cap.png"))
            {
                //保存されたキャプチャ画像の出力
                Cv2.ImShow("test1", cap);
            }
            */

            if (view_flag == true)
            {
                backgroundWorker1.RunWorkerAsync();
                view_flag = false;
                waveout.Resume();
            }

        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            Console.Write("pictureBox1_MouseMove\n");

            System.Drawing.Point p = new System.Drawing.Point();
            System.Drawing.Point start = new System.Drawing.Point();
            System.Drawing.Point end = new System.Drawing.Point();

            // 描画フラグcheck
            if (view_flag == false) return;

            p.X = e.X;
            p.Y = e.Y;

            // 座標から(X,Y)座標を計算
            GetRegion(MD, p, ref start, ref end);
            sqr_width = GetLength(start.X, end.X);
            sqr_height = GetLength(start.Y, end.Y);


            // ドラッグした領域を矩形で囲む
            Cv2.Rectangle(img, new Rect(start.X, start.Y, sqr_width, sqr_height), new Scalar(0, 0, 255), 3);
            bmp = BitmapConverter.ToBitmap(img);
            graphic.DrawImage(bmp, 0, 0, img.Cols, img.Rows);
            bmp.Dispose();
            img.Dispose();
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);
            img = BitmapConverter.ToMat(bmp_init);

        }

        private void GetRegion(System.Drawing.Point p1, System.Drawing.Point p2, ref System.Drawing.Point start, ref System.Drawing.Point end)
        {
            start.X = Math.Min(p1.X, p2.X);
            start.Y = Math.Min(p1.Y, p2.Y);

            end.X = Math.Max(p1.X, p2.X);
            end.Y = Math.Max(p1.Y, p2.Y);
        }

        private int GetLength(int start, int end)
        {
            return Math.Abs(start - end);
        }


        private string RunOCR(string filePath)
        {
            //string filePath = @"C:\cs_source\img\cap.png";
            string ocr_text = "";

            // Load an image from a local file.
            var image = Google.Cloud.Vision.V1.Image.FromFile(filePath);
            var client = ImageAnnotatorClient.Create();
            var response = client.DetectDocumentText(image);

            if (response != null)
            {
                foreach (var page in response.Pages)
                {
                    //Console.WriteLine(string.Join("\n", page));

                    foreach (var block in page.Blocks)
                    {
                        foreach (var paragraph in block.Paragraphs)
                        {
                            foreach (var word in paragraph.Words)
                            {
                                foreach (var symbol in word.Symbols)
                                {
                                    ocr_text = ocr_text + symbol.Text;
                                    //Console.WriteLine(string.Join("symbol:",symbol.Text)); //１文字ずつ出力
                                }
                                //Console.WriteLine(string.Join("Text::", text, "\n"));
                            }
                            //Console.WriteLine(string.Join("\n", paragraph.Words));

                        }
                    }
                }
            }
            Console.WriteLine("Text::" + ocr_text + "\n");

            //テキストボックスに文字を追加する
            //richTextBox1.AppendText(text+"\n");
            //richTextBox1.AppendText("---------------------------------------------------\n");

            return ocr_text;
        }

        public static class Test
        {
            //static async Task API_Main(string[] args)
            public static async Task view_api_token()
            {
                //アクセストークン取得
                await ApiController.obtainAccessToken();
                Console.WriteLine("アクセストークンを取得します。");
                Console.WriteLine("アクセストークン=" + ApiController.accessToken);

            }

            //static async Task API_Main(string[] args)
            public static async Task view_recognize_api()
            {

                // 機械翻訳API呼び出し          
                //string text = "你好";
                //string text = "おはよう";
                //string text = "おはようございます";

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(view_text, source_lang, target_lang);
                Console.WriteLine(view_text + " の翻訳結果：" + ApiController.result);
                view_text_result = ApiController.result;

            }

            //static async Task API_Main(string[] args)
            public static async Task view_voice_translate_api()
            {

                // 機械翻訳API呼び出し          
                //string text = "你好";
                //string text = "おはよう";
                //string text = "おはようございます";

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(view_voice_text, source_lang, target_lang);
                Console.WriteLine(view_text + " の翻訳結果：" + ApiController.result);
                view_voice_text_result = ApiController.result; //音声を翻訳した結果が入る


            }

            //static async Task API_Main(string[] args)
            public static async Task view_voice_api()
            {

                // 音声認識API呼び出し
                string path_to_audio = @"C:\cs_source\img\onephrase.wav";
                Console.WriteLine("音声認識APIを呼び出します。");
                await ApiController.speechRecognition(path_to_audio);
                Console.WriteLine(path_to_audio + " ファイルの翻訳結果：\n" + ApiController.result);
                view_voice_text = ApiController.result; //音声を認識した結果が入る
            }

            //static async Task API_Main(string[] args)
            public static async Task view_both_voice_translate_api(string path_to_audio)
            {

                // 音声認識API呼び出し
                //string path_to_audio = @"C:\cs_source\img\onephrase.wav";
                Console.WriteLine("音声認識APIを呼び出します。");
                await ApiController.speechRecognition(path_to_audio);
                Console.WriteLine(path_to_audio + " ファイルの翻訳結果：\n" + ApiController.result);
                view_voice_text = ApiController.result; //音声を認識した結果が入る

                string source_lang = "ja";
                string target_lang = "en";
                Console.WriteLine("機械翻訳APIを呼び出します。");
                await ApiController.machineTranslation(view_voice_text, source_lang, target_lang);
                Console.WriteLine(view_text + " の翻訳結果：" + ApiController.result);
                //Console.WriteLine(text + " の翻訳結果：" + ApiController.detail_result);
                view_voice_text_result = ApiController.result; //音声を翻訳した結果が入る
            }

        }

        public static class ApiController
        {
            // HTTP クライアント
            private static readonly HttpClient httpClient;
            // アクセストークン
            public static string accessToken = "";
            // 翻訳結果格納変数
            public static string result = "";
            //public static string detail_result = "";
            // アクセストークン取得用のパラメータ
            private static string token_uri = "https://auth.mimi.fd.ai/v2/token";
            private static string client_id = "09fe33e8218c4e23ac352eccba0bd8f5";
            private static string client_secret = "31aee58bd7400e44355e08e78afd702421bf0677dab719094e58a36442c80b560022eb24651867bb406f325b4cc2feb5c64a61cde0b61536f025a08f88579af8eef6ebe12bacbf166384f29fc42aa4c501576eea666c2ac46bed1623676a62cea87291458224134495c9f02de3ea28e65f00eece2615cda9dbdc06a210f5024a85362050a4efe7b7be93dd8bc920117bcabd7b1b0adb7645c7ed8336c15baad56b7f99c5c36c9a24c10445a1b16ac1bde44f336796695afb6c645143765b8c0b22b0b509bbec8bbf2a9c054589a14f18ca6e9a454f9a6ff015b89ac2adb1b30d2b3760051f1eb6495ab9af861dd4eb41811f73b356b2d8b2a8f439a420719534";
            private static string scope = "https://apis.mimi.fd.ai/auth/nict-tts/http-api-service;https://apis.mimi.fd.ai/auth/nict-tra/http-api-service;https://apis.mimi.fd.ai/auth/nict-asr/http-api-service;https://apis.mimi.fd.ai/auth/nict-asr/websocket-api-service;https://apis.mimi.fd.ai/auth/applications.r";
            private static string grant_type = "https://auth.mimi.fd.ai/grant_type/application_credentials";
            // 音声認識API
            private static string speech_recognition_uri = "https://service.mimi.fd.ai";
            // 機械翻訳API
            private static string machine_translation_uri = "https://sandbox-mt.mimi.fd.ai/machine_translation";

            static ApiController()
            {
                // クライアントの初期化は一回のみ（通信毎に初期化しない）
                httpClient = new HttpClient();
            }

            public static async Task obtainAccessToken()
            {
                // POSTリクエストのパラメータ
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", client_id },
                {"client_secret", client_secret },
                {"scope", scope },
                {"grant_type", grant_type }
            });

                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(token_uri, content);
                    response.EnsureSuccessStatusCode();

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    // レスポンス（文字列で表現された JSON ）をデシリアライズ
                    AuthAPI authAPI = JsonSerializer.Deserialize<AuthAPI>(responseBody);
                    accessToken = authAPI.accessToken;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }

            public static async Task machineTranslation(string text, string source_lang, string target_lang)
            {
                // POSTリクエストのパラメータ
                var parameter = new Dictionary<string, string>
            {
                {"text", text },
                {"source_lang", source_lang },
                {"target_lang", target_lang }
            };
                var content = new FormUrlEncodedContent(parameter);
                var uri = new Uri(machine_translation_uri);
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Content = content;

                try
                {
                    HttpResponseMessage response = await httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    string stringUnicode = responseBody.Replace("[\"", " ").Replace("\"]", " ").Trim();
                    Console.WriteLine("レスポンス=" + stringUnicode);

                    // Unicode文字列を日本語の文字列に変換する
                    byte[] bytesUnicode = Encoding.Unicode.GetBytes(stringUnicode);
                    result = Encoding.Unicode.GetString(bytesUnicode);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }
            public static async Task speechRecognition(string path_to_audio)
            {
                // ファイル読み込み
                byte[] binary = File.ReadAllBytes(path_to_audio);
                // POSTリクエスト
                var uri = new Uri(speech_recognition_uri);
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                var content = new ByteArrayContent(binary);

                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Headers.Add("x-mimi-process", "nict-asr");
                request.Headers.Add("x-mimi-input-language", "ja");
                request.Content = content;
                request.Content.Headers.Add("Content-Type", "audio/x-pcm;bit=16;rate=44100;channels=1");
                request.Content.Headers.Add("Content-Length", binary.Length.ToString());

                Console.WriteLine(request.ToString());

                try
                {
                    Console.WriteLine("POSTリクエストを送信します。");
                    HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    Console.WriteLine("レスポンスを受信します。");

                    // レスポンスを受け取る
                    string responseBody = await response.Content.ReadAsStringAsync();
                    result = responseBody;
                    //detail_result = responseBody;
                    MimiASR mimiASR = JsonSerializer.Deserialize<MimiASR>(responseBody);

                    result = "";
                    for (int i = 0; i < mimiASR.response.Length; i++)
                    {
                        result += mimiASR.response[i].result.Split('|')[0];
                        //detail_result += i.ToString() + ":" + mimiASR.response[i].result + "\n";
                    }
                    result = result + "\n";
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\n 例外を検出しました。");
                    Console.WriteLine("エラーメッセージ :{0} ", e.Message);
                }

            }
        }

        public class AuthAPI
        {
            public int startTimestamp { set; get; }
            public string selfLink { set; get; }
            public string kind { set; get; }
            public int code { set; get; }
            public string status { set; get; }
            public int expires_in { set; get; }
            public string targetLink { set; get; }
            public string operationId { set; get; }
            public string error { set; get; }
            public string accessToken { set; get; }
            public int endTimestamp { set; get; }
            public int progress { set; get; }
        }

        public class MimiASR
        {
            public Response[] response { set; get; }
            public class Response
            {
                public string result { set; get; }
            }
            public string session_id { set; get; }
            public string status { set; get; }
            public string type { set; get; }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //画面を閉じる
            this.Close();
        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw2 = (BackgroundWorker)sender;

            bw2.ReportProgress(0);

        }

        async private void backgroundWorker2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // 音声の翻訳
            string onephrase_file_path = @"C:\cs_source\my_awesome_lib\onephrase\";
            string[] file_paths = Directory.GetFiles(onephrase_file_path, "*", SearchOption.TopDirectoryOnly);
            foreach (string file_path in file_paths)
            {
                //ファイル名の取得
                string file_name = Path.GetFileNameWithoutExtension(file_path);
                Console.WriteLine(file_name);

                // 翻訳
                await Test.view_both_voice_translate_api(file_path);

                //テキストボックスに文字を追加する
                //if (view_voice_text_result != "" || view_voice_text != "")
                if (view_voice_text_result != "")
                {
                    richTextBox2.AppendText(view_voice_text);
                    richTextBox2.AppendText(view_voice_text_result + "\n");
                    richTextBox2.AppendText("--------------\n");
                }

                view_voice_text = "";
                view_voice_text_result = "";

            }
        }

        private void view_Load(object sender, EventArgs e)
        {

        }
    }
}
