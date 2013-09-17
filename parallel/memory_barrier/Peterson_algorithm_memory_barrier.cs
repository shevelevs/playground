using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

// for details on memory access order and memory barriers
// see http://bartoszmilewski.com/2008/11/05/who-ordered-memory-fences-on-an-x86/


namespace interlocked
{
    abstract class CriticalSection
    {
        public abstract void enter(int p);
        public abstract void leave(int p);
    }

    class NaiveCriticalSection: CriticalSection
    {
        bool [] entered = new bool[2];

        public NaiveCriticalSection()
        {
            entered[0] = entered[1] = false;
        }

        public override void enter(int p)
        {
            while (entered[1 - p])
                ; // wait
            entered[p] = true;
        }

        public override void leave(int p)
        {
            entered[p] = false;
        }
    }

    class PetersonCriticalSection: CriticalSection
    {
        int turn = 0;
        bool[] entered = new bool[2];

        public PetersonCriticalSection()
        {
            entered[0] = entered[1] = false;
        }

        public override void enter(int p)
        {
            int x = 1 - p;
            entered[p] = true;
            Thread.MemoryBarrier();
            turn = x;
            while (entered[x] && turn == x)
                ;
        }

        public override void leave(int p)
        {
            entered[p] = false;
        }
    }

    class CountingThread
    {
        int m_p;
        CriticalSection m_cs;
        public CountingThread(int p, CriticalSection cs)
        {
            m_p = p;
            m_cs = cs;
        }

        public static void ThreadMethod(object self)
        {
            int p = ((CountingThread)self).m_p;
            CriticalSection cs = ((CountingThread)self).m_cs;

            // Create 100,000 instances of CountClass. 
            for (int i = 0; i < 100000; i++) {
                cs.enter(p);
                CountClass.inc();
                cs.leave(p);

                cs.enter(p);
                CountClass.dec();
                cs.leave(p);
            }
        }
    }

    class Test
    {
        static void Main()
        {
            Thread thread1 = new Thread(CountingThread.ThreadMethod);
            Thread thread2 = new Thread(CountingThread.ThreadMethod);
            CriticalSection cs = new PetersonCriticalSection();
            thread1.Start(new CountingThread(0, cs));
            thread2.Start(new CountingThread(1, cs));
            thread1.Join();
            thread2.Join();

            Console.WriteLine("UnsafeInstanceCount: {0}" +
                "\nSafeCountInstances: {1}",
                CountClass.UnsafeInstanceCount.ToString(),
                CountClass.SafeInstanceCount.ToString());
        }

    }

    class CountClass
    {
        static int unsafeInstanceCount = 0;
        static int   safeInstanceCount = 0;

        static public int UnsafeInstanceCount
        {
            get {return unsafeInstanceCount;}
        }

        static public int SafeInstanceCount
        {
            get {return safeInstanceCount;}
        }

        public static void inc()
        {
            unsafeInstanceCount++;
            Interlocked.Increment(ref safeInstanceCount);
        }

        public static void dec()
        {
            unsafeInstanceCount--;
            Interlocked.Decrement(ref safeInstanceCount);
        }
    }
}
