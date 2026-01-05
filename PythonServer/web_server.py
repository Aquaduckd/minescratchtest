#!/usr/bin/env python3
"""
Web server for visualizing Minecraft server state.
Provides a live 3D view of entities and players in coordinate space.
"""

from flask import Flask, render_template, jsonify, request
import threading
import time
import os

# Get the directory where this file is located
current_dir = os.path.dirname(os.path.abspath(__file__))
template_dir = os.path.join(current_dir, 'templates')

app = Flask(__name__, template_folder=template_dir)

# This will be set by the main server to share world state
world_state = None

@app.route('/')
def index():
    """Main visualization page."""
    return render_template('index.html')

@app.route('/api/state')
def get_state():
    """API endpoint to get current world state."""
    if world_state is None:
        return {
            'players': [],
            'entities': [],
            'status': 'not_initialized'
        }
    
    # Extract player and entity data from world state
    players_data = []
    for player_uuid, player in world_state.players.items():
        players_data.append({
            'uuid': str(player_uuid),
            'x': player.x,
            'y': player.y,
            'z': player.z,
            'yaw': player.yaw,
            'pitch': player.pitch
        })
    
    entities_data = []
    for entity_id, entity in world_state.item_entities.items():
        # Get cached blocks for this entity
        cache = world_state.entity_collision_cache.get(entity_id, {})
        cached_blocks = list(cache.get('blocks_checked', [])) if cache else []
        
        entities_data.append({
            'id': entity_id,
            'x': entity.x,
            'y': entity.y,
            'z': entity.z,
            'velocity_x': entity.velocity_x,
            'velocity_y': entity.velocity_y,
            'velocity_z': entity.velocity_z,
            'item_id': entity.item_id,
            'count': entity.count,
            'cached_blocks': cached_blocks,
            'cached_result': cache.get('result', False) if cache else False,
            'gravity_disabled': cache.get('gravity_disabled', False) if cache else False
        })
    
    return {
        'players': players_data,
        'entities': entities_data,
        'status': 'active'
    }

@app.route('/api/pause', methods=['POST'])
def pause_updates():
    """Pause automatic entity updates."""
    if world_state is None:
        return jsonify({'error': 'World not initialized'}), 400
    world_state.pause_entity_updates()
    return jsonify({'status': 'paused'})

@app.route('/api/resume', methods=['POST'])
def resume_updates():
    """Resume automatic entity updates."""
    if world_state is None:
        return jsonify({'error': 'World not initialized'}), 400
    world_state.resume_entity_updates()
    return jsonify({'status': 'resumed'})

@app.route('/api/step', methods=['POST'])
def step_tick():
    """Step forward one tick of entity updates."""
    if world_state is None:
        return jsonify({'error': 'World not initialized'}), 400
    entity_count_before = len(world_state.item_entities)
    world_state.step_entity_tick()
    entity_count_after = len(world_state.item_entities)
    return jsonify({
        'status': 'stepped',
        'entity_count_before': entity_count_before,
        'entity_count_after': entity_count_after
    })

@app.route('/api/check_line_intersection', methods=['POST'])
def check_line_intersection():
    """Check if a line segment intersects any solid block."""
    if world_state is None:
        return jsonify({'error': 'World not initialized'}), 400
    
    data = request.get_json()
    if not data or 'line_start' not in data or 'line_end' not in data:
        return jsonify({'error': 'Missing line_start or line_end'}), 400
    
    line_start = tuple(data['line_start'])
    line_end = tuple(data['line_end'])
    return_debug = data.get('debug', False)
    
    result = world_state.check_line_intersects_solid_block(line_start, line_end, return_debug=return_debug)
    if return_debug:
        intersects, debug_info = result
        return jsonify({
            'intersects': intersects,
            'debug': debug_info
        })
    else:
        return jsonify({'intersects': result})

@app.route('/api/heightmap')
def get_heightmap():
    """Get height map data for a chunk."""
    if world_state is None:
        return jsonify({'error': 'World not initialized'}), 400
    
    chunk_x = request.args.get('chunk_x', type=int, default=0)
    chunk_z = request.args.get('chunk_z', type=int, default=0)
    
    # Check if terrain generation is enabled
    block_manager = world_state.block_manager
    if block_manager.terrain_generator is None:
        return jsonify({
            'chunk_x': chunk_x,
            'chunk_z': chunk_z,
            'height_map': None,
            'use_terrain': False,
            'message': 'Terrain generation not enabled'
        })
    
    # Get height map from terrain generator
    height_map = block_manager.terrain_generator.generate_height_map(chunk_x, chunk_z)
    
    # Convert to flat list for easier handling (16x16 = 256 entries)
    # Order: x increases fastest, then z (same as block data ordering)
    height_map_flat = []
    for z in range(16):
        for x in range(16):
            height_map_flat.append(height_map[z][x])
    
    return jsonify({
        'chunk_x': chunk_x,
        'chunk_z': chunk_z,
        'height_map': height_map_flat,  # 256 entries
        'height_map_2d': height_map,  # 16x16 array for easier visualization
        'min_height': min(min(row) for row in height_map),
        'max_height': max(max(row) for row in height_map),
        'use_terrain': True
    })

def run_web_server(host='127.0.0.1', port=5000, world=None):
    """
    Run the web server in a separate thread.
    
    Args:
        host: Host to bind to (default: 127.0.0.1)
        port: Port to bind to (default: 5000)
        world: World instance to share state
    """
    global world_state
    world_state = world
    
    # Disable Flask's default request logging to reduce noise
    import logging
    log = logging.getLogger('werkzeug')
    log.setLevel(logging.ERROR)
    
    # Run Flask in a separate thread
    app.run(host=host, port=port, debug=False, use_reloader=False, threaded=True)

