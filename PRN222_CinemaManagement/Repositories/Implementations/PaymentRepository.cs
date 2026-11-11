using PRN222_CinemaManagement.Data;
using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.Interfaces;

namespace PRN222_CinemaManagement.Repositories.Implementations;

public class PaymentRepository(CinemaDbContext context)
    : GenericRepository<Payment>(context), IPaymentRepository
{
}

