using System;

namespace ImportSeries.Helpers
{
    public static class ErrorHandler
    {
        public static void AddError(Exception ex)
        {
            // Simple console logging for now - in a production scenario you'd want proper logging
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }

        public static void AddLog(string message)
        {
            Console.WriteLine($"Log: {message}");
        }
    }
}