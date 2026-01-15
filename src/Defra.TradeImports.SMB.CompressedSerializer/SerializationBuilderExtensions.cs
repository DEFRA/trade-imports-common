using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using System.Text.Json;

namespace Defra.TradeImports.SMB.CompressedSerializer;

public static class SerializationBuilderExtensions
{
	/// <summary>
	/// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static TBuilder AddCompressedJsonSerializer<TBuilder>(
		this TBuilder builder,
		JsonSerializerOptions? options = null
	)
		where TBuilder : ISerializationBuilder
	{
		builder.RegisterSerializer<CompressedJsonMessageSerializer>(services =>
		{
			// Add the implementation
			services.TryAddSingleton(svp => new CompressedJsonMessageSerializer(
				options ?? svp.GetService<JsonSerializerOptions>()
			));
			// Add the serializer as IMessageSerializer<string>
			services.TryAddSingleton(svp =>
				svp.GetRequiredService<CompressedJsonMessageSerializer>() as IMessageSerializer<string>
			);
		});
		return builder;
	}

	/// <summary>
	/// Registers the <see cref="IMessageSerializer"/> with implementation as <see cref="JsonMessageSerializer"/>.
	/// </summary>
	/// <param name="builder"></param>
	/// <param name="options"></param>
	/// <returns></returns>
	public static TBuilder AddStringSerializer<TBuilder>(
		this TBuilder builder,
		JsonSerializerOptions? options = null
	)
		where TBuilder : ISerializationBuilder
	{
		builder.RegisterSerializer<ToStringSerializer>(services =>
		{
			// Add the implementation
			services.TryAddSingleton(svp => new ToStringSerializer());
			// Add the serializer as IMessageSerializer<string>
			services.TryAddSingleton(svp =>
				svp.GetRequiredService<ToStringSerializer>() as IMessageSerializer<string>
			);
		});
		return builder;
	}
}