using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImportSeries.Models;

namespace ImportSeries.Services
{
    public interface IPendingTransactionService
    {
        Task AddAsync(PendingTransaction transaction);
        Task<List<PendingTransaction>> GetAllAsync();
        Task RemoveAsync(Guid transactionId);
        Task UpdateAsync(PendingTransaction transaction);
    }
}