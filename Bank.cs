using System;
using System.Diagnostics;
using System.Threading;

namespace Bank
{
    public class Bank
    {
        private const int AccountCount = 10;
        private const int StartBalance = 10_000;
        private const int Operator = 6;
        private const int TransferOperator = 100_000;
        private const int MaxTransver = 5000;

        private readonly int[] Balances;
        private readonly object GlobalLocker = new object();
        private readonly object[] accountLocks;

        private long TotalErrors;
        private long NegativeBalances;
        private long FloatingSumErrors;

        private void Reset()
        {
            for (int i = 0; i < AccountCount; i++)
            {
                Balances[i] = StartBalance;
                TotalErrors = 0;
                NegativeBalances = 0;
                FloatingSumErrors = 0;
            }
        }
        public Bank()
        {
            Balances = new int[AccountCount];
            for (int i = 0; i < AccountCount; i++)
                Balances[i] = StartBalance;

            accountLocks = new object[AccountCount];
            for (int i = 0; i < AccountCount; i++)
                accountLocks[i] = new object();
        }
        public void RunInterlockedVersion()
        {
            Reset();
            Console.WriteLine("=== Interlocked (без синхронизации баланса) ===");

            Thread[] operators = new Thread[Operator];
            Random random = new Random(Environment.TickCount);

            for (int i = 0; i < Operator; i++)
            {
                operators[i] = new Thread(() =>
                {
                    var rdn = new Random(random.Next());
                    for (int j = 0; j < TransferOperator; j++)
                    {
                        int undo = rdn.Next(AccountCount);
                        int quo = rdn.Next(AccountCount);
                        if (undo == quo) continue;

                        int amount = rdn.Next(1, MaxTransver + 1);

                        int oldUndo = Balances[undo];
                        if (oldUndo >= amount)
                        {
                            Interlocked.Add(ref Balances[undo], -amount);
                            Interlocked.Add(ref Balances[quo], amount);
                        }
                        else
                        {
                            Interlocked.Increment(ref TotalErrors);
                        }
                    }
                });
                operators[i].Start();
            }
            var stopwatch = Stopwatch.StartNew();
            var auditor = new Thread(() =>
            {
                while (stopwatch.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(100);

                    long sum = 0;
                    for (int i = 0; i < AccountCount; i++)
                        sum += Balances[i];

                    for (int i = 0; i < AccountCount; i++)
                        if (Balances[i] < 0)
                            Interlocked.Increment(ref NegativeBalances);

                    if (sum != (long)AccountCount * StartBalance)
                        Interlocked.Increment(ref FloatingSumErrors);
                }
            });
            auditor.Start();
            foreach (var a in operators)
                a.Join();
            auditor.Join();

            stopwatch.Stop();

            long finalSum = 0;
            foreach (var b in Balances)
                finalSum += b;

            Console.WriteLine($" Время: {stopwatch.ElapsedMilliseconds} мс");
            Console.WriteLine($" Ошибочные попытки: {TotalErrors}");
            Console.WriteLine($" Минусовый баланс: {NegativeBalances}");
            Console.WriteLine($" Ошибки сумм (плавающие): {FloatingSumErrors}");
            Console.WriteLine($"Ошибки аудитора: {FloatingSumErrors}");
            Console.WriteLine($" Финальная сумма: {finalSum} (ожидается: {(long)AccountCount * StartBalance})");
            Console.WriteLine($" Статус: {(finalSum == (long)AccountCount * StartBalance ? "ОК" : "ERROR: Нарушение")}\n");
        }
        public void RunGlobalLockVersion()
        {
            Reset();
            Console.WriteLine("===  Глобальная блокировака (Monitor.Enter) ===");
            var operators = new Thread[Operator];
            Random random = new Random(Environment.TickCount);
            
            for (int i = 0; i < Operator; i++)
            {
                operators[i] = new Thread(() =>
                {
                    var rdn = new Random(random.Next());
                    for (int j = 0; j < TransferOperator; j++)
                    {
                        int undo = rdn.Next(AccountCount);
                        int quo = rdn.Next(AccountCount);
                        if (undo == quo) continue;

                        int amount = rdn.Next(1, MaxTransver + 1);

                        bool lockTaken = false;
                        try
                        {
                            Monitor.Enter(GlobalLocker, ref lockTaken);
                            if (Balances[undo] >= amount)
                            {
                                Balances[undo] -= amount;
                                Balances[quo] += amount;
                            }
                            else
                            {
                                Interlocked.Increment(ref TotalErrors);
                            }
                        }
                        finally
                        {
                            if (lockTaken) Monitor.Exit(GlobalLocker);
                        }
                    }
                });
                operators[i].Start();
            }
            var stopwatch = Stopwatch.StartNew();
            var auditor = new Thread(() =>
            {
                while (stopwatch.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(100);

                    long sum = 0;
                    bool lockTaken = false;
                    try
                    {
                        Monitor.Enter(GlobalLocker, ref lockTaken);
                        for (int i = 0; i < AccountCount; i++)
                            sum += Balances[i];
                    }
                    finally
                    {
                        if (lockTaken) Monitor.Exit(GlobalLocker);
                    }

                    if (sum != (long)AccountCount * StartBalance)
                        Interlocked.Increment(ref FloatingSumErrors);
                }
            });
            auditor.Start();
            foreach (var a in operators)
                a.Join();
            auditor.Join();

            stopwatch.Stop();
            long finalSum = 0;
            foreach (var b in Balances)
                finalSum += b;

            Console.WriteLine($" Время: {stopwatch.ElapsedMilliseconds} мс");
            Console.WriteLine($" Ошибочные попытки: {TotalErrors}");
            Console.WriteLine($" Ошибки аудитора: {FloatingSumErrors}");
            Console.WriteLine($" Финальная сумма: {finalSum} ");
            Console.WriteLine($" Статус: {(finalSum == (long)AccountCount * StartBalance ? "ОК" : "ERROR: Нарушение")}\n");
        }

        //3
        public void RunPerAccountLockVersion()
        {
            Reset();
            Console.WriteLine("=== Блокировка счета ===");

            var operators = new Thread[Operator];
            Random random = new Random(Environment.TickCount);

            for (int i = 0; i < Operator; i++)
            {
                operators[i] = new Thread(() =>
                {
                    var rdn = new Random(random.Next());
                    for (int j = 0; j < TransferOperator; j++)
                    {
                        int undo = rdn.Next(AccountCount);
                        int quo = rdn.Next(AccountCount);
                        if (undo == quo) continue;

                        int amount = rdn.Next(1, MaxTransver + 1);

                        int firstMany = Math.Min(undo, quo);
                        int secondMany = Math.Max(undo, quo);

                        bool lock1Taken = false;
                        bool lock2Taken = false;

                        try
                        {
                            Monitor.Enter(accountLocks[firstMany], ref lock1Taken);
                            Monitor.Enter(accountLocks[secondMany], ref lock2Taken);

                            if (Balances[undo] > amount)
                            {
                                Balances[undo] -= amount;
                                Balances[quo] += amount;
                            }
                            else
                            {
                                Interlocked.Increment(ref TotalErrors);
                            }
                        }
                        finally
                        {
                            if (lock1Taken) Monitor.Exit(accountLocks[firstMany]);
                            if (lock2Taken) Monitor.Exit(accountLocks[secondMany]);
                        }
                    }
                });
                operators[i].Start();
            }
            var stopwatch = Stopwatch.StartNew();
            var auditor = new Thread(() =>
            {
                while (stopwatch.ElapsedMilliseconds < 2000)
                {
                    Thread.Sleep(100);

                    long sum = 0;
                    bool[] tacken = new bool[AccountCount];

                    try
                    {
                        for (int i = 0; i < AccountCount; i++)
                            Monitor.Enter(accountLocks[i], ref tacken[i]);
                        for (int i = 0; i < AccountCount; i++)
                            sum += Balances[i];
                    }
                    finally
                    {
                        for (int i = AccountCount - 1; i >= 0; i--)
                            if (tacken[i])
                            {
                                Monitor.Exit(accountLocks[i]);
                            }
                    }

                    if (sum != (long)AccountCount * StartBalance)
                        Interlocked.Increment(ref FloatingSumErrors);
                }
            });
            auditor.Start();
            foreach (var a in operators)
                a.Join();
            auditor.Join();

            stopwatch.Stop();
            long finalSum = 0;
            foreach (var b in Balances)
                finalSum += b;

            Console.WriteLine($" Время: {stopwatch.ElapsedMilliseconds} мс");
            Console.WriteLine($" Ошибочные попытки: {TotalErrors}");
            Console.WriteLine($" Ошибки аудитора: {FloatingSumErrors}");
            Console.WriteLine($" Финальная сумма: {finalSum} ");
            Console.WriteLine($" Статус: {(finalSum == (long)AccountCount * StartBalance ? "ОК" : "ERROR: Нарушение")}\n");
        }
        //4
        public void RunDangerousLockVersion()
        {
            Reset();
            Console.WriteLine("=== Опасная блокировка (lock on this) ===");

            var operators = new Thread[Operator];
            Random random = new Random(Environment.TickCount);

            for (int i = 0; i < Operator; i++)
            {
                operators[i] = new Thread(() =>
                {
                    var rdn = new Random(random.Next());
                    for (int j = 0; j < TransferOperator; j++)
                    {
                        int undo = rdn.Next(AccountCount);
                        int quo = rdn.Next(AccountCount);
                        if (undo == quo) continue;

                        int amount = rdn.Next(1, MaxTransver + 1);

                        lock (this)
                        {
                            if (Balances[undo] > amount)
                            {
                                Balances[undo] -= amount;
                                Balances[quo] += amount;
                            }
                            else
                            {
                                Interlocked.Increment(ref TotalErrors);
                            }
                        }
                    }
                });
                operators[i].Start();
            }
            var stopwatch = Stopwatch.StartNew();
            var blockBanc = new Thread(() =>
            {
                Console.WriteLine(" Блокировка банка на 3 секунды...");
                lock (this) 
                {
                    Thread.Sleep(3000);
                }
                Console.WriteLine(" Освобождение от блокировки");
            });
            blockBanc.Start();

            var auditor = new Thread(() =>
            {
                while (stopwatch.ElapsedMilliseconds < 4000)
                {
                    Thread.Sleep(100);
                    long sum = 0;
                    lock (this)
                    {
                        for (int i = 0; i < AccountCount; i++) 
                            sum += Balances[i];
                    }
                    if (sum != (long)AccountCount * StartBalance)
                        Interlocked.Increment(ref FloatingSumErrors);
                }
            });
            auditor.Start();
            foreach (var a in operators)
                a.Join();
            auditor.Join();

            stopwatch.Stop();
            long finalSum = 0;
            foreach (var b in Balances)
                finalSum += b;

            Console.WriteLine($" Время: {stopwatch.ElapsedMilliseconds} мс");
            Console.WriteLine($" Ошибочные попытки: {TotalErrors}");
            Console.WriteLine($" Ошибки аудитора: {FloatingSumErrors}");
            Console.WriteLine($" Финальная сумма: {finalSum} ");
            Console.WriteLine($" Статус: {(finalSum == (long)AccountCount * StartBalance ? "ОК" : "ERROR: Нарушение")}\n");
        }
    }
}
