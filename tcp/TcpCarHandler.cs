using System.Collections;
using UnityEngine;
using System;
using UnityEngine.UI;
using System.Globalization;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;
using System.Collections.Generic;


namespace tk
{

    public class TcpCarHandler : MonoBehaviour
    {

        public GameObject carObj;

        public ICar car;


        public PathManager pm;
        public CameraSensor camSensor;
        public DepthSensor lidarSensor;

        private tk.JsonTcpClient client;
        public Text ai_text;
        // TODO: changed
        public float limitFPS = 20.0f;
        float timeSinceLastCapture = 0.0f;

        float steer_to_angle = 25.0f;

        float ai_steering = 0.0f;
        float ai_throttle = 0.0f;
        float ai_brake = 0.0f;

        bool asynchronous = true;
        float time_step = 0.1f;
        bool bResetCar = false;
        bool bExitScene = false;
        Dictionary<string, GameObject> obstacleList = new Dictionary<string, GameObject>();

        public enum State
        {
            UnConnected,
            SendTelemetry
        }

        public State state = State.UnConnected;
        State prev_state = State.UnConnected;

        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)limitFPS;

            car = carObj.GetComponent<ICar>();
            pm = GameObject.FindObjectOfType<PathManager>();

            Canvas canvas = GameObject.FindObjectOfType<Canvas>();
            GameObject go = CarSpawner.getChildGameObject(canvas.gameObject, "AISteering");
            
            if (go != null)
                ai_text = go.GetComponent<Text>();
        }

        public void Init(tk.JsonTcpClient _client)
        {
            client = _client;

            if (client == null)
            {
                Debug.Log("Initial Client is null");
                return;
            }

            client.dispatchInMainThread = false; //too slow to wait.
            client.dispatcher.Register("control", new tk.Delegates.OnMsgRecv(OnControlsRecv));
            client.dispatcher.Register("tracking", new tk.Delegates.OnMsgRecv(OnTrackingRecv));
            client.dispatcher.Register("exit_scene", new tk.Delegates.OnMsgRecv(OnExitSceneRecv));
            client.dispatcher.Register("reset_car", new tk.Delegates.OnMsgRecv(OnResetCarRecv));
            client.dispatcher.Register("new_car", new tk.Delegates.OnMsgRecv(OnRequestNewCarRecv));
            client.dispatcher.Register("step_mode", new tk.Delegates.OnMsgRecv(OnStepModeRecv));
            client.dispatcher.Register("quit_app", new tk.Delegates.OnMsgRecv(OnQuitApp));
            client.dispatcher.Register("regen_road", new tk.Delegates.OnMsgRecv(OnRegenRoad));
            client.dispatcher.Register("car_config", new tk.Delegates.OnMsgRecv(OnCarConfig));
            client.dispatcher.Register("cam_config", new tk.Delegates.OnMsgRecv(OnCamConfig));
            client.dispatcher.Register("connected", new tk.Delegates.OnMsgRecv(OnConnected));
            client.dispatcher.Register("load_scene", new tk.Delegates.OnMsgRecv(OnLoad));
            client.dispatcher.Register("pid", new tk.Delegates.OnMsgRecv(OnPidStart));
            client.dispatcher.Register("obstacle", new tk.Delegates.OnMsgRecv(OnObstacleRecv));


            
            Debug.Log("Finished Car Handler init");
        }

        public void Start()
        {
            SendCarLoaded();
            state = State.SendTelemetry;
            Debug.Log("Started Car Handler");
        }

        public tk.JsonTcpClient GetClient()
        {
            return client;
        }

        public void OnDestroy()
        {
            if (client)
                client.dispatcher.Reset();
        }

        void Disconnect()
        {
            client.Disconnect();
        }

        void SendTelemetry()
        {
            if (client == null)
                return;

            JSONObject json = new JSONObject(JSONObject.Type.OBJECT);
            json.AddField("msg_type", "telemetry");

            json.AddField("steering_angle", car.GetSteering() / steer_to_angle);
            json.AddField("throttle", car.GetThrottle());
            json.AddField("speed", car.GetVelocity().magnitude);
            json.AddField("image", Convert.ToBase64String(camSensor.GetImageBytes()));
            if (lidarSensor == null)
            {
                lidarSensor = carObj.transform.GetComponentInChildren<DepthSensor>();
            }

            Debug.Log(lidarSensor.GetAlive());
            json.AddField("depth", Convert.ToBase64String(lidarSensor.GetImageBytes()));

            json.AddField("hit", car.GetLastCollisionName());
            car.ClearLastCollision();

            Transform tm = car.GetTransform();
            json.AddField("pos_x", tm.position.x);
            json.AddField("pos_y", tm.position.y);
            json.AddField("pos_z", tm.position.z);
            json.AddField("quat_2", tm.rotation.x);
            json.AddField("quat_3", tm.rotation.y);
            json.AddField("quat_4", tm.rotation.z);
            json.AddField("quat_1", tm.rotation.w);

            json.AddField("time", Time.timeSinceLevelLoad);

            json.AddField("lap", StatsDisplayer.getLap());
            json.AddField("sector", StatsDisplayer.getCurrentWaypoint());

            if (pm != null)
            {
                json.AddField("sector", pm.path.iActiveSpan);
                float cte = 0.0f;
                if (pm.path.GetCrossTrackErr(tm.position, ref cte))
                {
                    json.AddField("cte", cte);
                }
                else
                {
                    pm.path.ResetActiveSpan();
                    json.AddField("cte", 0.0f);
                }
                json.AddField("maxSector", pm.path.getMaxWayPoints());
            }

            client.SendMsg(json);
        }

        void SendCarLoaded()
        {
            Debug.Log("sendin car loaded");
            if (client == null)
                return;

            JSONObject json = new JSONObject(JSONObject.Type.OBJECT);
            json.AddField("msg_type", "car_loaded");
            client.SendMsg(json);
            Debug.Log("car loaded.");
        }

        void OnControlsRecv(JSONObject json)
        {
            try
            {
                ai_steering = float.Parse(json["steering"].str, CultureInfo.InvariantCulture.NumberFormat) * steer_to_angle;
                ai_throttle = float.Parse(json["throttle"].str, CultureInfo.InvariantCulture.NumberFormat);
                ai_brake = float.Parse(json["brake"].str, CultureInfo.InvariantCulture.NumberFormat);

                car.RequestSteering(ai_steering);
                car.RequestThrottle(ai_throttle);
                car.RequestFootBrake(ai_brake);
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        void OnConnected(JSONObject json)
        {
            try
            {
                Debug.Log("connected feedback");
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        void OnLoad(JSONObject json)
        {
            try
            {
               Debug.Log("load_scene request");
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        void OnPidStart(JSONObject json)
        {
            try
            {
                Debug.Log("PID request");
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        


        void OnTrackingRecv(JSONObject json)
        {
            try
            {

                float car_x = float.Parse(json["x"].str, CultureInfo.InvariantCulture.NumberFormat);
                float car_y = float.Parse(json["y"].str, CultureInfo.InvariantCulture.NumberFormat);
                float car_angle = float.Parse(json["angle"].str, CultureInfo.InvariantCulture.NumberFormat);
                // Debug.Log("Set pose from external script");
                // Debug.Log(car_x.ToString());
                // Debug.Log(car_y.ToString());
                // Debug.Log(car_angle.ToString());
                UnityMainThreadDispatcher.Instance().Enqueue(MoveCar(car_x,car_y,car_angle));
                
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        void OnObstacleRecv(JSONObject json)
        {
            try
            {
                string obs_name = json.GetField("name").str;
                float obs_x = float.Parse(json["x"].str, CultureInfo.InvariantCulture.NumberFormat);
                float obs_y = float.Parse(json["y"].str, CultureInfo.InvariantCulture.NumberFormat);
                float obs_z = float.Parse(json["z"].str, CultureInfo.InvariantCulture.NumberFormat);
                float obs_angle = float.Parse(json["angle1"].str, CultureInfo.InvariantCulture.NumberFormat);
                float obs_angle2 = float.Parse(json["angle2"].str, CultureInfo.InvariantCulture.NumberFormat);
                float obs_angle3 = float.Parse(json["angle3"].str, CultureInfo.InvariantCulture.NumberFormat);
                UnityMainThreadDispatcher.Instance().Enqueue(MoveObs(obs_name,obs_x,obs_y,obs_z,obs_angle,obs_angle2,obs_angle3));
                
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }

        IEnumerator MoveCar(float car_x, float car_y, float car_angle)
        {
            
            car.Set(new Vector3(car_x, 0.5653478f, car_y), Quaternion.Euler(0f, car_angle, 0f));
            yield return null;
        }

        IEnumerator MoveObs(string name, float obs_x, float obs_y, float obs_z, float obs_angle, float obs_angle2, float obs_angle3)
        {
            Debug.Log(name);
            // Debug.Log("Implement obstacle logic");
            if (obstacleList.ContainsKey(name))
            {
                Debug.Log("found instance");
                GameObject obstacle = obstacleList[name];
                Vector3 newPosition = new Vector3(obs_x, obs_z, obs_y);
                obstacle.transform.position = newPosition;
                obstacle.transform.rotation = Quaternion.Euler(obs_angle, obs_angle2, obs_angle3);
            }
            else
            {
                Debug.Log("new instance");
                GameObject obstacle = GameObject.Find(name);
                Debug.Log(obstacle);
                Vector3 newPosition = new Vector3(obs_x, obs_z, obs_y);
                obstacle.transform.position = newPosition;
                obstacle.transform.rotation = Quaternion.Euler(obs_angle, obs_angle2, obs_angle3);
                obstacleList.Add(name,obstacle);
            }
            // car.Set(new Vector3(car_x, 0.5653478f, car_y), Quaternion.Euler(0f, car_angle, 0f));
            yield return null;
        }

        void OnExitSceneRecv(JSONObject json)
        {
            bExitScene = true;
        }

        void ExitScene()
        {
            SceneManager.LoadSceneAsync(0);
        }

        void OnResetCarRecv(JSONObject json)
        {
            bResetCar = true;
        }

        void OnRequestNewCarRecv(JSONObject json)
        {
            tk.JsonTcpClient client = null; //TODO where to get client?

            //We get this callback in a worker thread, but need to make mainthread calls.
            //so use this handy utility dispatcher from
            // https://github.com/PimDeWitte/UnityMainThreadDispatcher
            UnityMainThreadDispatcher.Instance().Enqueue(SpawnNewCar(client));
        }

        IEnumerator SpawnNewCar(tk.JsonTcpClient client)
        {
            CarSpawner spawner = GameObject.FindObjectOfType<CarSpawner>();

            if (spawner != null)
            {
                spawner.Spawn(client);
            }

            yield return null;
        }

        void OnRegenRoad(JSONObject json)
        {
            //This causes the track to be regenerated with the given settings.
            //This only works in scenes that have random track generation enabled.
            float turn_increment = 0;
            string wayPointsString = json.GetField("wayPoints").str;
            string[] wayPoints = new string[0];
            if (!string.IsNullOrEmpty(wayPointsString))
            {
                wayPoints = wayPointsString.Split("@");
            }


            //We get this callback in a worker thread, but need to make mainthread calls.
            //so use this handy utility dispatcher from
            // https://github.com/PimDeWitte/UnityMainThreadDispatcher
            UnityMainThreadDispatcher.Instance().Enqueue(RegenRoad(turn_increment, wayPoints));
        }

        IEnumerator RegenRoad(float turn_increment, string[] wayPoints)
        {
            // TrainingManager train_mgr = GameObject.FindObjectOfType<TrainingManager>();
            // PathManager path_mgr = GameObject.FindObjectOfType<PathManager>();
            Debug.Log("Calling Regen Road");
            if (pm != null)
            {
                Debug.Log("Train manager is not null");
                if (turn_increment != 0.0 && pm != null)
                {
                    pm.turnInc = turn_increment;
                }
                int style=1;
                RoadGen(wayPoints,style);
                
                // train_mgr.OnMenuRegenTrack();
            }
            else
            {
                Debug.Log("pm is null");
            }

            yield return null;
        }

        void RoadGen(string[] wayPoints,int style)
        {
            Debug.Log("We start a new run in tcp client");
            car.RestorePosRot();
            if (wayPoints.Length > 1)
            {
                Debug.Log("We create a new road");
                pm.DestroyRoad();
                RoadBuilder roadBuilder;
                roadBuilder = GameObject.FindObjectOfType<RoadBuilder>();
                roadBuilder.SetNewRoadVariation(style);
                pm.InitNewRoad(wayPoints);
                
            }
            pm.path.ResetActiveSpan();

            car.RequestFootBrake(1);
        }

        void OnCarConfig(JSONObject json)
        {
            Debug.Log("Got car config message");

            string body_style = json.GetField("body_style").str;
            int body_r = int.Parse(json.GetField("body_r").str);
            int body_g = int.Parse(json.GetField("body_g").str);
            int body_b = int.Parse(json.GetField("body_b").str);
            string car_name = json.GetField("car_name").str;
            int font_size = 100;

            if (json.GetField("font_size") != null)
                font_size = int.Parse(json.GetField("font_size").str);

            if (carObj != null)
                UnityMainThreadDispatcher.Instance().Enqueue(SetCarConfig(body_style, body_r, body_g, body_b, car_name, font_size));
        }

        IEnumerator SetCarConfig(string body_style, int body_r, int body_g, int body_b, string car_name, int font_size)
        {
            CarConfig conf = carObj.GetComponent<CarConfig>();

            if (conf)
            {
                conf.SetStyle(body_style, body_r, body_g, body_b, car_name, font_size);
            }

            yield return null;
        }

        void OnCamConfig(JSONObject json)
        {
            float fov = float.Parse(json.GetField("fov").str, CultureInfo.InvariantCulture.NumberFormat);
            float offset_x = float.Parse(json.GetField("offset_x").str, CultureInfo.InvariantCulture.NumberFormat);
            float offset_y = float.Parse(json.GetField("offset_y").str, CultureInfo.InvariantCulture.NumberFormat);
            float offset_z = float.Parse(json.GetField("offset_z").str, CultureInfo.InvariantCulture.NumberFormat);
            float rot_x = float.Parse(json.GetField("rot_x").str, CultureInfo.InvariantCulture.NumberFormat);
            float fish_eye_x = float.Parse(json.GetField("fish_eye_x").str, CultureInfo.InvariantCulture.NumberFormat);
            float fish_eye_y = float.Parse(json.GetField("fish_eye_y").str, CultureInfo.InvariantCulture.NumberFormat);
            int img_w = int.Parse(json.GetField("img_w").str);
            int img_h = int.Parse(json.GetField("img_h").str);
            int img_d = int.Parse(json.GetField("img_d").str);
            string img_enc = json.GetField("img_enc").str;

            if (carObj != null)
                UnityMainThreadDispatcher.Instance().Enqueue(SetCamConfig(fov, offset_x, offset_y, offset_z, rot_x, img_w, img_h, img_d, img_enc, fish_eye_x, fish_eye_y));
        }

        IEnumerator SetCamConfig(float fov, float offset_x, float offset_y, float offset_z, float rot_x,
            int img_w, int img_h, int img_d, string img_enc, float fish_eye_x, float fish_eye_y)
        {
            Debug.Log("Camera config");
            CameraSensor camSensor = carObj.transform.GetComponentInChildren<CameraSensor>();

            if (camSensor)
            {
                camSensor.SetConfig(fov, offset_x, offset_y, offset_z, rot_x, img_w, img_h, img_d, img_enc);

                Fisheye fe = camSensor.gameObject.GetComponent<Fisheye>();

                if (fe != null && (fish_eye_x != 0.0f || fish_eye_y != 0.0f))
                {
                    fe.enabled = true;
                    fe.strengthX = fish_eye_x;
                    fe.strengthY = fish_eye_y;
                }
            }

            yield return null;
        }

        void OnStepModeRecv(JSONObject json)
        {
            string step_mode = json.GetField("step_mode").str;
            float _time_step = float.Parse(json.GetField("time_step").str);

            Debug.Log("got settings");

            if (step_mode == "synchronous")
            {
                Debug.Log("setting mode to synchronous");
                asynchronous = false;
                this.time_step = _time_step;
                Time.timeScale = 0.0f;
            }
            else
            {
                Debug.Log("setting mode to asynchronous");
                asynchronous = true;
            }
        }

        void OnQuitApp(JSONObject json)
        {
            Application.Quit();
        }

        // Update is called once per frame
        void Update()
        {
            if (bExitScene)
            {
                bExitScene = false;
                ExitScene();
            }

            if (state == State.SendTelemetry)
            {
                if (bResetCar)
                {
                    car.RestorePosRot();
                    pm.path.ResetActiveSpan();
                    bResetCar = false;
                }


                timeSinceLastCapture += Time.deltaTime;

                if (timeSinceLastCapture > 1.0f / limitFPS)
                {
                    timeSinceLastCapture -= (1.0f / limitFPS);
                    SendTelemetry();
                }

                if (ai_text != null)
                    ai_text.text = string.Format("NN: steering->{0} : throttle->{1}", ai_steering, ai_throttle);

            }
        }
    }
}
