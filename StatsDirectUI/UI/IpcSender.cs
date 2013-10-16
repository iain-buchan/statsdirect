using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;

namespace StatsDirect.UI
{
    class IpcSender
    {
        /// <summary>
        /// If another StatsDirect is open, this tells it to open the specified file and returns true.  If none is open, we have to open the file; return false;
        /// </summary>
        public static bool TryToTellAnotherStatsDirectToOpen(string path)
        {
            // If the path length is too long to handle (assuming worse than worst-case UTF-8 byte encoding and lengths), handle it locally.
            // See http://en.wikipedia.org/wiki/UTF-8 for the divide-by-six; this is a very paranoid assumption as no such code points were ever allocated.
            if (path.Length > IpcListener.MAXIMUM_PATH_LENGTH / 6 - 20)
                return false;

            bool wasCreated;
            using (Semaphore semaphore = new Semaphore(1, 1, IpcListener.WAIT_SEMAPHORE_NAME, out wasCreated))
            {
                // If the semaphore was created new, there can't be another process at the far end.
                if (wasCreated)
                    return false;

                // Otherwise, something's there.  Either we're very unlucky and have hit another probe like this one, or a StatsDirect process is waiting to handle the call.
                using (MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting(IpcListener.MEMORY_FILE_NAME))
                {
                    using (Semaphore accessSemaphore = new Semaphore(1, 1, IpcListener.MEMORY_SEMAPHORE_NAME))
                    {
                        accessSemaphore.WaitOne();
                        try
                        {
                            using (MemoryMappedViewStream s = memoryMappedFile.CreateViewStream())
                            {
                                StreamWriter sw = new StreamWriter(s);
                                sw.WriteLine(path);
                                sw.WriteLine(); // Just to ensure there's extra space available
                                sw.Flush();
                            }
                        }
                        finally
                        {
                            // Don't let an exception prevent us releasing the access semaphore.
                            accessSemaphore.Release();
                        }
                    }
                }
                semaphore.Release();

                return true;
            }
        }
    }
}
