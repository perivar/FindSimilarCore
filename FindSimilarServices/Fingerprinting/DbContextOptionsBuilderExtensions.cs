using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FindSimilarServices.Fingerprinting
{
    public static class DbContextOptionsBuilderExtensions
    {
        public static DbContextOptionsBuilder UseSerilog(this DbContextOptionsBuilder optionsBuilder, ILoggerFactory loggerFactory, bool throwOnQueryWarnings = false)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);

            optionsBuilder.ConfigureWarnings(warnings =>
            {
                warnings.Log(RelationalEventId.TransactionError);

                /*
                if (throwOnQueryWarnings)
                {
                    warnings.Throw(RelationalEventId.QueryClientEvaluationWarning);
                    warnings.Throw(RelationalEventId.QueryPossibleExceptionWithAggregateOperator);
                    warnings.Throw(RelationalEventId.QueryPossibleUnintendedUseOfEqualsWarning);
                }
                else
                {
                    warnings.Log(RelationalEventId.QueryClientEvaluationWarning);
                    warnings.Log(RelationalEventId.QueryPossibleExceptionWithAggregateOperator);
                    warnings.Log(RelationalEventId.QueryPossibleUnintendedUseOfEqualsWarning);
                }
                */
            });

            return optionsBuilder;
        }
    }

}