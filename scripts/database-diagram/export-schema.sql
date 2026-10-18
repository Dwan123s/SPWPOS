SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

-- Catalog metadata only: no application rows or connection credentials.
SELECT (
    SELECT DB_NAME() AS databaseName, CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS serverName,
        CONVERT(varchar(33), SYSUTCDATETIME(), 126) + 'Z' AS capturedAt,
        JSON_QUERY((
            SELECT t.object_id AS id, s.name AS [schema], t.name,
                JSON_QUERY((
                    SELECT c.column_id AS id, c.name, ty.name AS type,
                        c.max_length AS maxLength, c.precision, c.scale,
                        c.is_nullable AS nullable, c.is_identity AS [identity], c.is_computed AS computed,
                        dc.definition AS defaultDefinition, cc.definition AS computedDefinition,
                        CAST(CASE WHEN EXISTS (
                            SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                            WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.column_id=c.column_id
                        ) THEN 1 ELSE 0 END AS bit) AS primaryKey
                    FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
                    LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
                    LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
                    WHERE c.object_id=t.object_id ORDER BY c.column_id FOR JSON PATH
                )) AS columns,
                JSON_QUERY((
                    SELECT i.name, i.is_primary_key AS primaryKey, i.is_unique AS [unique],
                        i.has_filter AS filtered, i.filter_definition AS filterDefinition, i.is_disabled AS disabled,
                        JSON_QUERY((
                            SELECT c.name, ic.key_ordinal AS ordinal
                            FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                            WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0
                            ORDER BY ic.key_ordinal FOR JSON PATH
                        )) AS columns
                    FROM sys.indexes i WHERE i.object_id=t.object_id AND i.is_unique=1 AND i.is_hypothetical=0
                    ORDER BY i.name FOR JSON PATH
                )) AS keys
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            WHERE t.is_ms_shipped=0 AND t.name NOT IN ('__EFMigrationsHistory','sysdiagrams')
            ORDER BY s.name,t.name FOR JSON PATH
        )) AS tables,
        JSON_QUERY((
            SELECT fk.object_id AS id, fk.name, fk.parent_object_id AS childTableId,
                fk.referenced_object_id AS parentTableId, fk.is_disabled AS disabled, fk.is_not_trusted AS notTrusted,
                fk.delete_referential_action_desc AS onDelete, fk.update_referential_action_desc AS onUpdate,
                JSON_QUERY((
                    SELECT fkc.constraint_column_id AS ordinal, pc.name AS childColumn, rc.name AS parentColumn
                    FROM sys.foreign_key_columns fkc
                    JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
                    JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
                    WHERE fkc.constraint_object_id=fk.object_id ORDER BY fkc.constraint_column_id FOR JSON PATH
                )) AS columns
            FROM sys.foreign_keys fk
            JOIN sys.tables pt ON pt.object_id=fk.parent_object_id
            JOIN sys.tables rt ON rt.object_id=fk.referenced_object_id
            WHERE pt.is_ms_shipped=0 AND rt.is_ms_shipped=0
                AND pt.name NOT IN ('__EFMigrationsHistory','sysdiagrams') AND rt.name NOT IN ('__EFMigrationsHistory','sysdiagrams')
            ORDER BY fk.name FOR JSON PATH
        )) AS foreignKeys
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
) AS schemaJson;
COMMIT TRANSACTION;
