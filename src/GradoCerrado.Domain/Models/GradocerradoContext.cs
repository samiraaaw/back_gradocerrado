using Microsoft.EntityFrameworkCore;

namespace GradoCerrado.Domain.Models;

public class GradocerradoContext : DbContext
{
    public GradocerradoContext(DbContextOptions<GradocerradoContext> options) : base(options)
    {
    }

    public DbSet<Estudiante> Estudiantes { get; set; }
    public DbSet<Area> Areas { get; set; }
    public DbSet<EstudianteConfiguracion> EstudianteConfiguraciones { get; set; }
    public DbSet<EstudianteNotificacionConfig> EstudianteNotificacionConfigs { get; set; }
    public DbSet<FragmentosQdrant> FragmentosQdrant { get; set; }
    public DbSet<ModalidadTest> ModalidadesTest { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }
    public DbSet<PreguntasGenerada> PreguntasGeneradas { get; set; }
    public DbSet<PromptsSistema> PromptsSistemas { get; set; }
    public DbSet<Tema> Temas { get; set; }
    public DbSet<Subtema> Subtemas { get; set; }
    public DbSet<TestPregunta> TestPreguntas { get; set; }
    public DbSet<Test> Tests { get; set; }
    public DbSet<TiposNotificacion> TiposNotificacion { get; set; }
    public DbSet<TiposPrompt> TiposPrompt { get; set; }
    public DbSet<TiposTest> TiposTest { get; set; }
    public DbSet<PreguntaOpcione> PreguntaOpciones { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(
                "tu_connection_string",
                o => o.MapEnum<TipoPregunta>("tipo_pregunta")
            );
        }
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Estudiante>(entity =>
        {
            entity.ToTable("estudiantes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            entity.Property(e => e.SegundoNombre).HasColumnName("segundo_nombre").HasMaxLength(100);
            entity.Property(e => e.ApellidoPaterno).HasColumnName("apellido_paterno").HasMaxLength(100);
            entity.Property(e => e.ApellidoMaterno).HasColumnName("apellido_materno").HasMaxLength(100);
            entity.Property(e => e.NombreCompleto).HasColumnName("nombre_completo").HasMaxLength(200).ValueGeneratedOnAddOrUpdate();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FotoPerfil).HasColumnName("foto_perfil").HasMaxLength(255);
            entity.Property(e => e.IdAvatarSeleccionado).HasColumnName("id_avatar_seleccionado").HasMaxLength(50);
            entity.Property(e => e.NivelActual).HasColumnName("nivel_actual").ValueGeneratedOnAdd();
            entity.Property(e => e.NivelDiagnosticado).HasColumnName("nivel_diagnosticado").ValueGeneratedOnAddOrUpdate();
            entity.Property(e => e.TestDiagnosticoCompletado).HasColumnName("test_diagnostico_completado").HasDefaultValue(false);
            entity.Property(e => e.FechaTestDiagnostico).HasColumnName("fecha_test_diagnostico").HasColumnType("timestamp without time zone");
            entity.Property(e => e.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("timestamp without time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UltimoAcceso).HasColumnName("ultimo_acceso").HasColumnType("timestamp without time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.Verificado).HasColumnName("verificado").HasDefaultValue(false);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.ToTable("areas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Icono).HasColumnName("icono");
            entity.Property(e => e.Importancia).HasColumnName("importancia");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<EstudianteConfiguracion>(entity =>
        {
            entity.ToTable("estudiante_configuracion");
            entity.HasKey(e => e.EstudianteId);
            entity.Property(e => e.EstudianteId).HasColumnName("estudiante_id");
            entity.Property(e => e.ObjetivoPreguntasDiarias).HasColumnName("objetivo_preguntas_diarias").HasDefaultValue(10);
            entity.Property(e => e.RecordatoriosActivos).HasColumnName("recordatorios_activos").HasDefaultValue(true);
            entity.Property(e => e.HorarioEstudioPreferido).HasColumnName("horario_estudio_preferido");
            entity.Property(e => e.FechaActualizacion).HasColumnName("fecha_actualizacion").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<EstudianteNotificacionConfig>(entity =>
        {
            entity.ToTable("estudiante_notificacion_config");
            entity.HasKey(e => e.EstudianteId);
            entity.Property(e => e.EstudianteId).HasColumnName("estudiante_id");
            entity.Property(e => e.NotificacionesHabilitadas).HasColumnName("notificaciones_habilitadas").HasDefaultValue(true);
            entity.Property(e => e.TokenDispositivo).HasColumnName("token_dispositivo");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("timestamp without time zone");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin").HasColumnType("timestamp without time zone");
            entity.Property(e => e.FechaActualizacion).HasColumnName("fecha_actualizacion").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<FragmentosQdrant>(entity =>
        {
            entity.ToTable("fragmentos_qdrant");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired();
            entity.Property(e => e.ChunkId).HasColumnName("chunk_id").IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.TemaId).HasColumnName("tema_id");
            entity.Property(e => e.UsadoEnPreguntas).HasColumnName("usado_en_preguntas").HasDefaultValue(0);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<ModalidadTest>(entity =>
        {
            entity.ToTable("modalidad_test");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.ToTable("notificaciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EstudianteId).HasColumnName("estudiante_id");
            entity.Property(e => e.TiposNotificacionId).HasColumnName("tipos_notificacion_id");
            entity.Property(e => e.Titulo).HasColumnName("titulo").IsRequired();
            entity.Property(e => e.Mensaje).HasColumnName("mensaje").IsRequired();
            entity.Property(e => e.DatosAdicionales).HasColumnName("datos_adicionales").HasDefaultValue("{}");
            entity.Property(e => e.FechaProgramada).HasColumnName("fecha_programada").HasColumnType("timestamp without time zone");
            entity.Property(e => e.Enviado).HasColumnName("enviado").HasDefaultValue(false);
            entity.Property(e => e.FechaEnviado).HasColumnName("fecha_enviado").HasColumnType("timestamp without time zone");
            entity.Property(e => e.Leido).HasColumnName("leido").HasDefaultValue(false);
            entity.Property(e => e.FechaLeido).HasColumnName("fecha_leido").HasColumnType("timestamp without time zone");
            entity.Property(e => e.AccionTomada).HasColumnName("accion_tomada").HasDefaultValue(false);
            entity.Property(e => e.FechaAccion).HasColumnName("fecha_accion").HasColumnType("timestamp without time zone");
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<TiposNotificacion>(entity =>
        {
            entity.ToTable("tipos_notificacion");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<TiposPrompt>(entity =>
        {
            entity.ToTable("tipos_prompt");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
        });

        modelBuilder.Entity<TiposTest>(entity =>
        {
            entity.ToTable("tipos_test");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<Tema>(entity =>
        {
            entity.ToTable("temas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
            entity.Property(e => e.NombreNorm).HasColumnName("nombre_norm");
        });

        modelBuilder.Entity<Subtema>(entity =>
        {
            entity.ToTable("subtemas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TemaId).HasColumnName("tema_id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").IsRequired();
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Orden).HasColumnName("orden").HasDefaultValue(0);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
            entity.HasOne(e => e.Tema).WithMany(t => t.Subtemas).HasForeignKey(e => e.TemaId);
        });

        modelBuilder.Entity<Test>(entity =>
        {
            entity.ToTable("tests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EstudianteId).HasColumnName("estudiante_id");
            entity.Property(e => e.ModalidadId).HasColumnName("modalidad_id");
            entity.Property(e => e.TipoTestId).HasColumnName("tipo_test_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<PreguntasGenerada>(entity =>
        {
            entity.ToTable("preguntas_generadas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasConversion(v => v, v => v).HasColumnType("tipo_pregunta").IsRequired();
            entity.Property(e => e.ModalidadId).HasColumnName("modalidad_id");
            entity.Property(e => e.Nivel).HasColumnName("nivel").HasConversion(v => v, v => v).HasColumnType("nivel_dificultad").IsRequired();
            entity.Property(e => e.TemaId).HasColumnName("tema_id");
            entity.Property(e => e.SubtemaId).HasColumnName("subtema_id");
            entity.Property(e => e.TextoPregunta).HasColumnName("texto_pregunta").IsRequired();
            entity.Property(e => e.RespuestaCorrectaBoolean).HasColumnName("respuesta_correcta_boolean");
            entity.Property(e => e.RespuestaCorrectaOpcion).HasColumnName("respuesta_correcta_opcion");
            entity.Property(e => e.RespuestaModelo).HasColumnName("respuesta_modelo");
            entity.Property(e => e.Explicacion).HasColumnName("explicacion");
            entity.Property(e => e.Activa).HasColumnName("activa").HasDefaultValue(true);
            entity.Property(e => e.CreadaPor).HasColumnName("creada_por");
            entity.Property(e => e.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("timestamp without time zone");
            entity.Property(e => e.FechaActualizacion).HasColumnName("fecha_actualizacion").HasColumnType("timestamp without time zone");
            entity.Property(e => e.VecesUtilizada).HasColumnName("veces_utilizada").HasDefaultValue(0);
            entity.Property(e => e.VecesCorrecta).HasColumnName("veces_correcta").HasDefaultValue(0);
            entity.Property(e => e.UltimoUso).HasColumnName("ultimo_uso").HasColumnType("timestamp without time zone");
            entity.Property(e => e.TasaAcierto).HasColumnName("tasa_acierto");
            entity.Property(e => e.PromptSistemaId).HasColumnName("prompt_sistema_id");
            entity.Property(e => e.ContextoFragmentos).HasColumnName("contexto_fragmentos");
            entity.Property(e => e.ModeloIa).HasColumnName("modelo_ia");
            entity.Property(e => e.CalidadEstimada).HasColumnName("calidad_estimada");
            entity.HasOne(e => e.Modalidad).WithMany(m => m.PreguntasGenerada).HasForeignKey(e => e.ModalidadId);
            entity.HasOne(e => e.Tema).WithMany(t => t.PreguntasGenerada).HasForeignKey(e => e.TemaId);
            entity.HasOne(e => e.Subtema).WithMany(s => s.PreguntasGenerada).HasForeignKey(e => e.SubtemaId);
        });

        modelBuilder.Entity<PreguntaOpcione>(entity =>
        {
            entity.ToTable("pregunta_opciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PreguntaGeneradaId).HasColumnName("pregunta_generada_id");
            entity.Property(e => e.Opcion).HasColumnName("opcion").IsRequired();
            entity.Property(e => e.TextoOpcion).HasColumnName("texto_opcion").IsRequired();
            entity.Property(e => e.EsCorrecta).HasColumnName("es_correcta").HasDefaultValue(false);
            entity.HasOne(e => e.PreguntaGenerada).WithMany(p => p.PreguntaOpciones).HasForeignKey(e => e.PreguntaGeneradaId);
        });
    }
}