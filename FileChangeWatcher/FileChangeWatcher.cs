﻿using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileChangeWatcher.ScoreSystem;

namespace FileChangeWatcher
{
    class FileChangeWatcher
    {
        /// <summary>
        /// 타이머 관련
        /// </summary>
        private bool isFirstChange = true;
        private const int WaitingTime = 1000 * 10;
        private Thread thread;
        private Timer timer;

        /// <summary>
        /// ScoreSystem 관련
        /// </summary>
        private ScoreSystem.ScoreSystem scoreSystem = new ScoreSystem.ScoreSystem();

        /// <summary>
        /// DBMS 관련
        /// </summary>
        private DBMS dbms = new DBMS();

        public FileChangeWatcher()
        {
            Console.WriteLine($"[실행 초기화 전체 파일] - {dbms.TotalFilesCount}");
        }

        public void Run()
        {
            try
            {
                var filesystemWatcher = new FileSystemWatcher(dbms.RootPath);

                /// FilesystemWatcher 내부버퍼(기본 8192(8KB)) 32KB로 설정
                /// 설정 이유: 기본값으로는 부족하여 버퍼오버플로우 에러 발생
                filesystemWatcher.InternalBufferSize = 65536;

                filesystemWatcher.NotifyFilter = NotifyFilters.Attributes
                                                | NotifyFilters.CreationTime
                                                | NotifyFilters.DirectoryName
                                                | NotifyFilters.FileName
                                                | NotifyFilters.LastAccess
                                                | NotifyFilters.LastWrite
                                                | NotifyFilters.Security
                                                | NotifyFilters.Size;

                filesystemWatcher.Changed += OnChanged;
                filesystemWatcher.Created += OnCreated;
                filesystemWatcher.Deleted += OnDeleted;
                filesystemWatcher.Renamed += OnRenamed;
                filesystemWatcher.Error += OnError;

                filesystemWatcher.IncludeSubdirectories = true;
                filesystemWatcher.EnableRaisingEvents = true;

                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
            }
            catch (ArgumentException)
            {
                Console.WriteLine("[Error] 경로를 찾을 수 없습니다.");
                Console.WriteLine("[System] 경로를 다시 설정해주세요");
                Console.Write("[Path] <- ");
                dbms.InitSettingFile(Console.ReadLine());

                Console.WriteLine("[System] 프로그램을 다시 실행시켜주십시오.");
            }

        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            Console.WriteLine($"<Changed>: {e.FullPath}");
            Console.WriteLine($"[Time]: {DateTime.Now.ToString()}");

            this.CheckWork();
            this.dbms.AddChangeFile(e.FullPath);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"<Created>: {e.FullPath}");
            Console.WriteLine($"[Time]: {DateTime.Now.ToString()}");

            this.CheckWork();
            this.dbms.AddChangeFile(e.FullPath);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"<Deleted>: {e.FullPath}");
            Console.WriteLine($"[Time]: {DateTime.Now.ToString()}");

            this.CheckWork();

            /// 삭제된 파일,폴더까지 변경점에 넣어야 하는지 의문
            //this.dbms.AddChangeFile(e.FullPath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            string filepath = e.FullPath;
            Console.WriteLine($"<Renamed>");
            Console.WriteLine($"    Old: {e.OldFullPath}");
            Console.WriteLine($"    New: {e.FullPath}");
            Console.WriteLine($"[Time]: {DateTime.Now.ToString()}");

            CustomHashTable.ChangeGetOriginPath.Put(e.FullPath, e.OldFullPath);
            this.CheckWork();
            this.dbms.AddChangeFile(e.FullPath);
        }

        private void OnError(object sender, ErrorEventArgs e) =>
            PrintException(e.GetException());

        private void PrintException(Exception ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"<Message>: {ex.Message}");
                Console.WriteLine("<Stacktrace>:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }

        /// <summary>
        /// 연속된 작업인지 확인하고 타이머를 생성하는 메소드
        /// </summary>
        private void CheckWork()
        {
            if (this.isFirstChange)
                this.isFirstChange = false;

            if (thread == null)
                thread = new Thread(SetTimer);

            else if (thread.ThreadState != ThreadState.Stopped || thread.ThreadState != ThreadState.Aborted)
            {
                thread.Abort();
                this.timer.Dispose();
                thread = new Thread(SetTimer);
            }

            thread.Start();
        }

        /// <summary>
        /// 연속된 작업의 끝을 알기 위한 타이머의 시간을 생성하는 메소드
        /// </summary>
        private void SetTimer()
        {
            AutoResetEvent autoResetEvent = new AutoResetEvent(false);
            this.timer = new Timer(this.TimerRun, autoResetEvent, WaitingTime, 0);

            autoResetEvent.WaitOne();
            this.timer.Dispose();
        }

        /// <summary>
        /// 연속된 작업의 끝을 알기 위한 타이머가 끝났을 때 작동하는 메소드
        /// </summary>
        private void TimerRun(Object stateInfo)
        {
            /// 테스트 코드
            Console.WriteLine("Timer Run Start");

            /// 플래그 초기화
            this.isFirstChange = true;

            /// 계산
            scoreSystem.Run();

            /// DB 초기화
            /// dbms.ResetChangeFileList() 실행 위치 이곳이 맞는가..?
            dbms.Init();
            Console.WriteLine($"TimeRun_TotalFileNumber - {dbms.TotalFilesCount}");
            dbms.ResetChangeFileList();

            /// 타이머 초기화
            AutoResetEvent autoResetEvent = (AutoResetEvent)stateInfo;
            autoResetEvent.Set();

            /// 테스트 코드
            Console.WriteLine("Timer Run End");
        }
    }
}
