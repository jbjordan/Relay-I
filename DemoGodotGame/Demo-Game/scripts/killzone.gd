extends Area2D

@onready var timer = $Timer

func _on_body_entered(body):
	var relay_log_dict = { "Event" : "PlayerDeath", "Location" : str(global_position), "LevelId" : "1" }
	GlobalLogger.log_game_event(relay_log_dict)
	print("You died!")
	Engine.time_scale = 0.5
	body.get_node("CollisionShape2D").queue_free()
	timer.start()


func _on_timer_timeout():
	Engine.time_scale = 1.0
	get_tree().reload_current_scene()
