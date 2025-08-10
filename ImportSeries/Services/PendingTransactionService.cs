using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImportSeries.Models;
using Newtonsoft.Json;

namespace ImportSeries.Services
{
    public class PendingTransactionService : IPendingTransactionService
    {
        private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "pending_transactions.json");
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private List<PendingTransaction> _transactions;

        private async Task LoadTransactionsAsync()
        {
            if (_transactions != null) return;

            await _semaphore.WaitAsync();
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    _transactions = JsonConvert.DeserializeObject<List<PendingTransaction>>(json) ?? new List<PendingTransaction>();
                }
                else
                {
                    _transactions = new List<PendingTransaction>();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SaveTransactionsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var json = JsonConvert.SerializeObject(_transactions, Formatting.Indented);
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AddAsync(PendingTransaction transaction)
        {
            await LoadTransactionsAsync();
            _transactions.Add(transaction);
            await SaveTransactionsAsync();
        }

        public async Task<List<PendingTransaction>> GetAllAsync()
        {
            await LoadTransactionsAsync();
            return _transactions.ToList();
        }

        public async Task RemoveAsync(Guid transactionId)
        {
            await LoadTransactionsAsync();
            var transaction = _transactions.FirstOrDefault(t => t.Id == transactionId);
            if (transaction != null)
            {
                _transactions.Remove(transaction);
                await SaveTransactionsAsync();
            }
        }

        public async Task UpdateAsync(PendingTransaction transaction)
        {
            await LoadTransactionsAsync();
            var existing = _transactions.FirstOrDefault(t => t.Id == transaction.Id);
            if (existing != null)
            {
                existing.RetryCount = transaction.RetryCount;
                // Update other properties as needed
                await SaveTransactionsAsync();
            }
        }
    }
}