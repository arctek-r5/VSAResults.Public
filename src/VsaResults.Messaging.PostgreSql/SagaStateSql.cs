namespace VsaResults.Messaging.PostgreSql;

/// <summary>
/// SQL constants for saga state persistence.
/// </summary>
internal static class SagaStateSql
{
    public const string EnsureTable = """
        CREATE TABLE IF NOT EXISTS saga_state (
            correlation_id uuid NOT NULL,
            saga_type text NOT NULL,
            current_state text NOT NULL,
            state_data jsonb NOT NULL,
            version integer NOT NULL DEFAULT 1,
            created_at timestamptz NOT NULL DEFAULT now(),
            modified_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (correlation_id, saga_type)
        );
        DO $$
        DECLARE existing_pk_name text;
        BEGIN
            SELECT c.conname
            INTO existing_pk_name
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid
            WHERE t.relname = 'saga_state'
              AND c.contype = 'p'
              AND pg_get_constraintdef(c.oid) <> 'PRIMARY KEY (correlation_id, saga_type)';

            IF existing_pk_name IS NOT NULL THEN
                EXECUTE format('ALTER TABLE saga_state DROP CONSTRAINT %I', existing_pk_name);
            END IF;

            IF NOT EXISTS (
                SELECT 1
                FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                WHERE t.relname = 'saga_state'
                  AND c.contype = 'p'
                  AND pg_get_constraintdef(c.oid) = 'PRIMARY KEY (correlation_id, saga_type)'
            ) THEN
                ALTER TABLE saga_state ADD PRIMARY KEY (correlation_id, saga_type);
            END IF;
        END $$;
        CREATE INDEX IF NOT EXISTS ix_saga_state_type_state
            ON saga_state (saga_type, current_state);
        """;

    public const string Get = """
        SELECT state_data AS StateData, version AS Version
        FROM saga_state
        WHERE correlation_id = @CorrelationId AND saga_type = @SagaType;
        """;

    public const string Insert = """
        INSERT INTO saga_state (correlation_id, saga_type, current_state, state_data, version, created_at, modified_at)
        VALUES (@CorrelationId, @SagaType, @CurrentState, @StateData::jsonb, 1, @Now, @Now);
        """;

    public const string Update = """
        UPDATE saga_state
        SET current_state = @CurrentState,
            state_data = @StateData::jsonb,
            version = version + 1,
            modified_at = @Now
        WHERE correlation_id = @CorrelationId
          AND saga_type = @SagaType
          AND version = @ExpectedVersion;
        """;

    public const string Delete = """
        DELETE FROM saga_state
        WHERE correlation_id = @CorrelationId AND saga_type = @SagaType;
        """;

    public const string QueryByState = """
        SELECT state_data
        FROM saga_state
        WHERE saga_type = @SagaType AND current_state = @StateName;
        """;
}
