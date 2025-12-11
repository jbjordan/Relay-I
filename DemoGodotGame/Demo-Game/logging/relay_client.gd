extends Node

const uuid_chars = "0123456789abcdef"
const relay_endpoint = "<Relay_Endpoint>

var http_request: HTTPRequest
var elapsed_time := 0.0
var guid
var start_time
var start_time_unix
var log_buffer

func _init() -> void:
	log_buffer = "["
	start_time = Time.get_datetime_string_from_system()
	start_time_unix = Time.get_unix_time_from_datetime_string(start_time)
	guid = uuid4()
	print("Generated GUID:", guid)
	http_request = HTTPRequest.new()
	add_child(http_request)
	http_request.request_completed.connect(_on_request_completed)
	# Create a new Timer instance
	var timer = Timer.new()

	timer.wait_time = 5.0        # seconds
	timer.one_shot = false       # false = repeats, true = runs once
	timer.autostart = true       # starts automatically when added

	add_child(timer)

	# Connect the timeout signal to a function
	timer.timeout.connect(Callable(self, "_on_timer_timeout"))

# TODO: Should elapsed_time be used, or should time difference be calculated for each call?
# _process is not called since this script is not attached to a node
func _process(delta):
	elapsed_time += delta
	if elapsed_time >= 5.0:
		print("5 seconds have passed!")
		elapsed_time = 0.0

## HTTP FUNCTIONS

func _on_request_completed(result, response_code, _headers, body):
	if result != HTTPRequest.RESULT_SUCCESS and response_code != 200:
		var json = JSON.parse_string(body.get_string_from_utf8())
		print("Error Sending Log")
		print(json)

func send_request(body):
	var headers = ["Content-Type: application/json"]
	
	http_request.request(
		relay_endpoint,
		headers,
		HTTPClient.METHOD_POST,
		body
	)

## CALLABLE FUNCTIONS

func log_game_info(body):
	var json_body = update_log(body)
	add_to_buffer(json_body)

func log_info(body):
	pass
	# ignore due to noise
	#var json_body = update_log(body)
	#add_to_buffer(json_body)

func log_error(body):
	var json_body = update_log(body)
	add_to_buffer(json_body)

## HELPER FUNCTIONS

func add_to_buffer(body):
	if len(log_buffer) > 3:
		log_buffer = log_buffer + ',' + '\n' + body
	else:
		log_buffer = log_buffer + body
	
func _on_timer_timeout():
	log_buffer = log_buffer + "]"
	print(log_buffer)
	send_request(log_buffer)
	log_buffer = "["
	print("Timer triggered!")

func log_start():
	var body = { "Event" : "StartSession"}
	var json_body = update_log(body)
	send_request(json_body)

func update_log(body):
	var curr_time = Time.get_datetime_string_from_system()
	var curr_time_unix = Time.get_unix_time_from_datetime_string(curr_time)
	var diff_seconds = curr_time_unix - start_time_unix
	
	body["Timestamp"] = curr_time
	body["ElapsedTime"] = diff_seconds
	body["TrackingID"] = guid
	
	var json_body = JSON.stringify(body)
	return json_body

static func uuid4() -> String:
	var result := ""
	for i in range(32):
		if i in [8, 12, 16, 20]: result += "-"
		if i == 12: result += "4"
		elif i == 16:
			var r := randi() % 16
			r = (r & 0x3) | 0x8 # variant bits
			result += uuid_chars[r]
		else:
			var r := randi() % 16
			result += uuid_chars[r]
	return result
