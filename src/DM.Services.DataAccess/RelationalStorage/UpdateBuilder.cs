using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DM.Services.DataAccess.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DM.Services.DataAccess.RelationalStorage;

/// <inheritdoc />
internal class UpdateBuilder<TEntity> : IUpdateBuilder<TEntity>
    where TEntity : class, new()
{
    private readonly Guid _id;
    private readonly IList<Action<TEntity, DbContext>> _efUpdateActions;
    private readonly IList<Func<UpdateDefinition<TEntity>>> _mongoUpdateActions;
    private bool _toDelete;

    /// <inheritdoc />
    public UpdateBuilder(Guid id)
    {
        _id = id;
        _efUpdateActions = new List<Action<TEntity, DbContext>>();
        _mongoUpdateActions = new List<Func<UpdateDefinition<TEntity>>>();
    }

    /// <inheritdoc />
    public IUpdateBuilder<TEntity> Field<TValue>(Expression<Func<TEntity, TValue>> field, TValue value)
    {
        if (_toDelete)
        {
            throw new UpdateBuilderException("Builder is configured to delete entity, you cannot modify it");
        }

        _efUpdateActions.Add((entity, dbContext) =>
        {
            SetPropertyValue(entity, field, value);
            dbContext.Entry(entity).Property(field).IsModified = true;
        });
        _mongoUpdateActions.Add(() => new UpdateDefinitionBuilder<TEntity>().Set(field, value));
        return this;
    }

    /// <inheritdoc />
    public bool HasChanges() => _toDelete || _efUpdateActions.Any();

    public IUpdateBuilder<TEntity> Delete()
    {
        if (_efUpdateActions.Any())
        {
            throw new UpdateBuilderException("Builder is configured to update entity, you cannot delete it");
        }

        _toDelete = true;
        return this;
    }

    /// <inheritdoc />
    public Guid AttachTo(DbContext dbContext)
    {
        var entity = new TEntity();
        var type = entity.GetType();
        var primaryKeyProperty = GetPrimaryKeyProperty();
        if (primaryKeyProperty == null)
        {
            throw new UpdateBuilderException($"No key property was found for entity {type.Name}");
        }
        var attachedEntry = dbContext.Set<TEntity>().Local.FirstOrDefault(entry => _id.Equals(primaryKeyProperty.GetValue(entry)));
        if (attachedEntry != null)
        {
            dbContext.Entry(attachedEntry).State = EntityState.Detached;
        }

        primaryKeyProperty.SetValue(entity, _id);

        if (_toDelete)
        {
            dbContext.Set<TEntity>().Attach(entity);
            dbContext.Entry(entity).State = EntityState.Deleted;
            return _id;
        }

        if (!_efUpdateActions.Any())
        {
            return _id;
        }

        dbContext.Set<TEntity>().Attach(entity);
        foreach (var updateAction in _efUpdateActions)
        {
            updateAction.Invoke(entity, dbContext);
        }

        return _id;
    }

    public async Task<Guid> UpdateFor(DmMongoClient mongoClient, bool upsert)
    {
        var entityType = typeof(TEntity);
        if (entityType.GetCustomAttribute<MongoCollectionNameAttribute>() == null)
        {
            throw new UpdateBuilderException($"Entity type {entityType.Name} is not a mongo collection type");
        }

        var primaryKeyProperty = GetPrimaryKeyProperty();

        if (!_mongoUpdateActions.Any())
        {
            return _id;
        }

        var updateDefinition = new UpdateDefinitionBuilder<TEntity>()
            .Combine(_mongoUpdateActions.Select(a => a.Invoke()));
        var filterDefinition = new FilterDefinitionBuilder<TEntity>()
            .Eq(primaryKeyProperty.Name, _id);
        await mongoClient.GetCollection<TEntity>()
            .UpdateOneAsync(filterDefinition, updateDefinition, new UpdateOptions {IsUpsert = upsert});

        return _id;
    }

    private static PropertyInfo GetPrimaryKeyProperty()
    {
        var entityType = typeof(TEntity);
        var propertyInfos = entityType.GetProperties();
        var keyAttributedProperty =
            propertyInfos.Where(i => i.GetCustomAttribute<KeyAttribute>() != null).ToArray();
        if (keyAttributedProperty.Length == 1)
        {
            return keyAttributedProperty.First();
        }

        var idProperty = entityType.GetProperty("Id");
        if (idProperty != null)
        {
            return idProperty;
        }

        var conventionIdProperty = entityType.GetProperty($"{entityType.Name}Id");
        if (conventionIdProperty != null)
        {
            return conventionIdProperty;
        }

        throw new UpdateBuilderException(
            $"Entity {entityType.Name} has no primary key properties or only has composite key");
    }

    private static void SetPropertyValue<TValue>(TEntity target,
        Expression<Func<TEntity, TValue>> memberLambda, TValue value)
    {
        MemberExpression memberExpression;
        switch (memberLambda.Body)
        {
            case MemberExpression body:
                memberExpression = body;
                break;
            case UnaryExpression unary:
                Expression expression = unary;
                do expression = ((UnaryExpression) expression).Operand;
                while (expression.NodeType == ExpressionType.Convert ||
                       expression.NodeType == ExpressionType.ConvertChecked);
                memberExpression = (MemberExpression) expression;
                break;
            default:
                return;
        }

        var property = (PropertyInfo) memberExpression.Member;
        property.SetValue(target, value);
    }
}