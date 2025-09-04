import getHealth from "./scenarios/get-health";
import getWeatherforecast from "./scenarios/get-weatherforecast";

export default () => {
    group("Endpoint [GET] /health", () => {
        getHealth();
    });
    sleep(1);
    
    group("Endpoint [GET] /weatherforecast", () => {
        getWeatherforecast();
    });
    sleep(1);
};