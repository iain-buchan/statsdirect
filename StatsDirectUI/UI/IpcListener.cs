using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace StatsDirect.UI
{
    /// <summary>
    /// This class spawns a thread that listens for IPC from other StatsDirect instances and processes the messages on receipt.
    /// </summary>
    static class IpcListener
    {
        public const string WAIT_SEMAPHORE_NAME = "StatsDirect3IPCWait";
        public const string MEMORY_SEMAPHORE_NAME = "StatsDirect3IPCMemoryAccess";
        public const string MEMORY_FILE_NAME = "StatsDirect3IPCMemoryFile";
        public const long MAXIMUM_PATH_LENGTH = 0x3000; // Bytes
        public const int IPC_TIMEOUT = 1000; // Time between IPC timeouts and hence checks that the thread should shut down (milliseconds).
        private static Semaphore waitSemaphore;
        private static MemoryMappedFile memoryMappedFile;
        private static bool shouldStopListening;
        private static bool listening;

        /// <summary>
        /// Notify this class that it should start listening.
        /// </summary>
        public static void StartListening()
        {
            if (listening)
                return;
            shouldStopListening = false;
            listening = true;
            bool wasCreated; // We don't use this but, if wasCreated is true, no other process presently knows about that mutex.  If wasCreated is false, some other process has got a handle on it (which shouldn't happen unless we happen to be created at the same time as another SD instance is trying to get hold of the mutex).
            waitSemaphore = new Semaphore(0, 1, WAIT_SEMAPHORE_NAME, out wasCreated);
            memoryMappedFile = MemoryMappedFile.CreateNew(MEMORY_FILE_NAME, MAXIMUM_PATH_LENGTH);
            (new Thread(ListenThreadTop) {Name = "IPC Listener", IsBackground = true }).Start();
        }

        /// <summary>
        /// Notify this class that it should stop listening.
        /// </summary>
        public static void StopListening()
        {
            if (!listening)
                return;
            shouldStopListening = true;
        }

        /// <summary>
        /// Runs on its own thread.  Sets up to be able to listen, listens until it is told to stop, then shuts down and removes the mutex.
        /// </summary>
        /// <remarks>Precondition: mutex is non-null.</remarks>
        private static void ListenThreadTop()
        {
            try
            {
                while (!shouldStopListening)
                {
                    bool signalled = waitSemaphore.WaitOne(IPC_TIMEOUT);
                    if (signalled)
                    {
                        // If we get here, there must have been a signal.
                        string path;
                        using (Semaphore accessSemaphore = new Semaphore(1, 1, MEMORY_SEMAPHORE_NAME))
                        {
                            accessSemaphore.WaitOne();
                            try
                            {
                                using (MemoryMappedViewStream s = memoryMappedFile.CreateViewStream())
                                {
                                    StreamReader sr = new StreamReader(s);
                                    path = sr.ReadLine();
                                }
                            }
                            finally
                            {
                                // Don't let an exception prevent us releasing the access semaphore.
                                accessSemaphore.Release();
                            }
                        }
                        // The mutex doesn't need to be released.  A different process will release the mutex when communicating with this one.
                        ReceiveFileOpen(path);
                    }
                }

                // If we get here, we should stop.
            }
            catch (Exception)
            {
                // TODO: How to log or tell the user that there's a problem here?
            }
            finally
            {
                // Whether caused by legitimate exit or by an exception, release our resources.
                // As a side effect, this *should* cause an exceptional condition here to make it look like no open server is running and hence cause other SDs to open files locally - causing an irritating case of multiple windows but no lost opens.
                Cleanup();
            }
        }

        private static void Cleanup()
        {
            waitSemaphore.Close();
            memoryMappedFile.Dispose();
            listening = false;
        }

        delegate void OpenFileCallback(string path);

        private static void ReceiveFileOpen(string path)
        {
            if (null != SdApplication.SoleInstance && null != SdApplication.SoleInstance.MainWindow)
            {
                SdApplication.SoleInstance.MainWindow.Invoke(new OpenFileCallback(UiFileOpen), path);
            }
        }

        private static void UiFileOpen(string path)
        {
            SdApplication.SoleInstance.MainWindow.OpenFile(path, false);
        }
    }
}
