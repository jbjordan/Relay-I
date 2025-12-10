extends Node

var custom_logger

class CustomLogger extends Logger:
	# Note that this method is not called for messages that use
	# `push_error()` and `push_warning()`, even though these are printed to stderr.
	func _log_message(message: String, error: bool) -> void:
		# Do something with `message`.
		# `error` is `true` for messages printed to the standard error stream (stderr) with `print_error()`.
		# Note that this method will be called from threads other than the main thread, possibly at the same
		# time, so you will need to have some kind of thread-safety as part of it, like a Mutex.
		var relay_log_dict = { "Event" : "Info", "message" : message, "error" : str(error) }
		GlobalLogger.log_info(relay_log_dict)

	func _log_error(
			function: String,
			file: String,
			line: int,
			code: String,
			rationale: String,
			editor_notify: bool,
			error_type: int,
			_script_backtraces: Array[ScriptBacktrace]
	) -> void:
		# Do something with the error. The error text is in `rationale`.
		# See the Logger class reference for details on other parameters.
		# Note that this method will be called from threads other than the main thread, possibly at the same
		# time, so you will need to have some kind of thread-safety as part of it, like a Mutex.
		var relay_error_dict = {
			"Event" : "Error",
			"function" : function,
			"file" : file,
			"line" : str(line),
			"code" : code,
			"error" : rationale,
			"editor_notify" : str(editor_notify),
			"error_type" : str(error_type)
		}
		print(relay_error_dict)
		GlobalLogger.log_error(relay_error_dict)
# Use `_init()` to initialize the logger as early as possible, which ensures that messages
# printed early are taken into account. However, even when using `_init()`, the engine's own
# initialization messages are not accessible.

const RelayClient = preload("res://logging/relay_client.gd")
var relay_client = RelayClient.new()

func _init() -> void:
	custom_logger = CustomLogger.new()
	OS.add_logger(custom_logger)
	add_child(relay_client)
	
func log_game_event(info):
	relay_client.log_game_info(info)

func log_info(body):
	relay_client.log_info(body)

func log_error(body):
	relay_client.log_error(body)
