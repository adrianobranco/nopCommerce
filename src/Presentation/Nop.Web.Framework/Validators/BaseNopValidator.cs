using System.Linq;
using System.Linq.Dynamic.Core;
using FluentValidation;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Extensions;
using Nop.Services.Localization;

namespace Nop.Web.Framework.Validators
{
    /// <summary>
    /// Base class for validators
    /// </summary>
    /// <typeparam name="TModel">Model type</typeparam>
    public abstract class BaseNopValidator<TModel> : AbstractValidator<TModel> where TModel : class
    {
        #region Ctor

        protected BaseNopValidator()
        {
            PostInitialize();
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Developers can override this method in custom partial classes in order to add some custom initialization code to constructors
        /// </summary>
        protected virtual void PostInitialize()
        {
        }

        /// <summary>
        /// Sets validation rule(s) to appropriate database model
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        /// <param name="filterStringPropertyNames">Properties to skip</param>
        protected virtual void SetDatabaseValidationRules<TEntity>(IDbContext dbContext, params string[] filterStringPropertyNames)
        {
            SetStringPropertiesMaxLength<TEntity>(dbContext, filterStringPropertyNames);
            SetDecimalMaxValue<TEntity>(dbContext);
        }

        /// <summary>
        /// Sets length validation rule(s) to string properties according to appropriate database model
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        /// <param name="filterPropertyNames">Properties to skip</param>
        protected virtual void SetStringPropertiesMaxLength<TEntity>(IDbContext dbContext, params string[] filterPropertyNames)
        {
            if (dbContext == null)
                return;
            
            //get max length of the string properties
            var names = typeof(TModel).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && !filterPropertyNames.Contains(p.Name))
                .Select(p => p.Name).ToList();
            var propertyMaxLengths = dbContext.GetColumnsMaxLength<TEntity>()
                .Where(property => names.Contains(property.Key) && property.Value.HasValue);

            //create expressions for the validation rules
            var maxLengthExpressions = propertyMaxLengths.Select(property => new
            {
                MaxLength = property.Value.Value,
                Expression = DynamicExpressionParser.ParseLambda<TModel, string>(null, false, property.Key)
            });

            //define validation rules
            foreach (var expression in maxLengthExpressions)
            {
                RuleFor(expression.Expression).Length(0, expression.MaxLength);
            }
        }

        /// <summary>
        /// Sets max value validation rule(s) to decimal properties according to appropriate database model
        /// </summary>
        /// <typeparam name="TEntity">Entity type</typeparam>
        /// <param name="dbContext">Database context</param>
        protected virtual void SetDecimalMaxValue<TEntity>(IDbContext dbContext)
        {
            if (dbContext == null)
                return;

            //get max value of the decimal properties
            var names = typeof(TModel).GetProperties()
                .Where(p => p.PropertyType == typeof(decimal))
                .Select(p => p.Name).ToList();
            var decimalPropertyMaxValues = dbContext.GetDecimalColumnsMaxValue<TEntity>()
                .Where(property => names.Contains(property.Key) && property.Value.HasValue);

            //create expressions for the validation rules
            var maxValueExpressions = decimalPropertyMaxValues.Select(property => new
            {
                MaxValue = property.Value.Value,
                Expression = DynamicExpressionParser.ParseLambda<TModel, decimal>(null, false, property.Key)
            });

            //define validation rules
            var localizationService = EngineContext.Current.Resolve<ILocalizationService>();
            foreach (var expression in maxValueExpressions)
            {
                RuleFor(expression.Expression).IsDecimal(expression.MaxValue)
                    .WithMessage(string.Format(localizationService.GetResource("Nop.Web.Framework.Validators.MaxDecimal"), expression.MaxValue - 1));
            }
        }

        #endregion
    }
}