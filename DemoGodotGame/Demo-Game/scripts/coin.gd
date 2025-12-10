extends Area2D

@onready var game_manager = %GameManager
@onready var animation_player = $AnimationPlayer

func _on_body_entered(body):
	var relay_log_dict = { "Event" : "CoinCollected", "Location" : str(global_position), "LevelId" : "1"  }
	GlobalLogger.log_game_event(relay_log_dict)
	game_manager.add_point()
	animation_player.play("pickup")
